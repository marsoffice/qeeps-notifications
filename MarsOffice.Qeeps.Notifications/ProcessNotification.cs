using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentValidation;
using MarsOffice.Qeeps.Access.Abstractions;
using MarsOffice.Qeeps.Notifications.Abstractions;
using MarsOffice.Qeeps.Notifications.Entities;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using SendGrid;
using SendGrid.Helpers.Mail;
using WebPush;

namespace MarsOffice.Qeeps.Notifications
{
    public class ProcessNotification
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _accessClient;
        private readonly ISendGridClient _sendGridClient;
        private readonly WebPushClient _webPushClient;
        private readonly IValidator<RequestNotificationDto> _validator;
        private readonly IEnumerable<Template> _templates;
        private readonly VapidDetails _vapidDetails;

        public ProcessNotification(IConfiguration config, IHttpClientFactory httpClientFactory,
            ISendGridClient sendGridClient, IValidator<RequestNotificationDto> validator)
        {
            _config = config;
            _accessClient = httpClientFactory.CreateClient("access");
            _sendGridClient = sendGridClient;
            _webPushClient = new WebPushClient();
            _validator = validator;
            _templates = _config.GetValue<IEnumerable<Template>>("Templates");
            _vapidDetails = new VapidDetails(_config["VapidSubject"], _config["VapidPublicKey"], _config["VapidPrivateKey"]);
        }

        [FunctionName("ProcessNotification")]
        public async Task Run(
            [ServiceBusTrigger("notifications", Connection = "sbconnectionstring")] RequestNotificationDto dto,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "Notifications",
                #if DEBUG
                CreateIfNotExists = true,
                PartitionKey = "/UserId",
                #endif
                ConnectionStringSetting = "cdbconnectionstring")] IAsyncCollector<NotificationEntity> notificationsOut,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "PushSubscriptions",
                #if DEBUG
                CreateIfNotExists = true,
                PartitionKey = "UserId",
                #endif
                ConnectionStringSetting = "cdbconnectionstring")] DocumentClient pushSubscriptionsClient
            )
        {
            await _validator.ValidateAndThrowAsync(dto);

            var foundTemplates = _templates.Where(x => x.Name == dto.TemplateName).ToList();
            if (!foundTemplates.Any())
            {
                throw new Exception("Bad template name");
            }

            if (dto.PlaceholderData != null && dto.PlaceholderData.Any())
            {
                foreach (var t in foundTemplates)
                {
                    foreach (var kvp in dto.PlaceholderData)
                    {
                        t.Title = t.Title.Replace("{{" + kvp.Key + "}}", kvp.Value);
                        t.Message = t.Message.Replace("{{" + kvp.Key + "}}", kvp.Value);
                    }
                }
            }

            var getUsersResponse = await _accessClient.PostAsJsonAsync("/api/access/users", dto.RecipientUserIds);
            getUsersResponse.EnsureSuccessStatusCode();
            var getUsersJson = await getUsersResponse.Content.ReadAsStringAsync();
            var userDtos = JsonConvert.DeserializeObject<IEnumerable<UserDto>>(getUsersJson, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

            if (userDtos == null || !userDtos.Any())
            {
                return;
            }

            var validUserIds = userDtos.Select(x => x.Id).Distinct().ToList();

            IEnumerable<UserPreferencesDto> preferences = new List<UserPreferencesDto>();

            var userPreferencesResponse = await _accessClient.PostAsJsonAsync("/api/access/preferences", validUserIds);
            if (userPreferencesResponse.IsSuccessStatusCode)
            {
                var userPreferencesJson = await userPreferencesResponse.Content.ReadAsStringAsync();
                preferences = JsonConvert.DeserializeObject<IEnumerable<UserPreferencesDto>>(userPreferencesJson, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
            }

            foreach (var userDto in userDtos)
            {
                var lang = "ro";
                var foundPref = preferences.FirstOrDefault(x => x.UserId == userDto.Id);
                if (foundPref != null && !string.IsNullOrEmpty(foundPref.PreferredLanguage))
                {
                    lang = foundPref.PreferredLanguage;
                }
                else
                {
                    if (!string.IsNullOrEmpty(dto.PreferredLanguage))
                    {
                        lang = dto.PreferredLanguage;
                    }
                }

                var foundTemplate = foundTemplates.Single(x => x.Language == lang);

                if (dto.NotificationTypes.Contains(NotificationType.Email) && !string.IsNullOrEmpty(userDto.Email))
                {
                    var sgm = new SendGridMessage();
                    sgm.SetSubject(foundTemplate.Title);
                    sgm.SetFrom(_config["FromName"], _config["FromEmail"]);
                    sgm.AddTo(userDto.Email);
                    sgm.AddContent(MimeType.Html, foundTemplate.Message);
                    await _sendGridClient.SendEmailAsync(sgm);
                }

                if (dto.NotificationTypes.Contains(NotificationType.InApp))
                {
                    var notificationEntity = new NotificationEntity
                    {
                        UserId = userDto.Id,
                        AbsoluteRouteUrl = dto.AbsoluteRouteUrl,
                        CreatedDate = DateTimeOffset.UtcNow,
                        IsRead = false,
                        Message = foundTemplate.Message,
                        Title = foundTemplate.Title,
                        ReadDate = null,
                        Severity = dto.Severity
                    };

                    await notificationsOut.AddAsync(notificationEntity);

                    // TODO SIGNALR

                    // PUSH
                    var col = UriFactory.CreateDocumentCollectionUri("notifications", "PushSubscriptions");
                    var pushSubscriptionsQuery = pushSubscriptionsClient.CreateDocumentQuery<PushSubscriptionEntity>(col, new FeedOptions
                    {
                        PartitionKey = new PartitionKey(userDto.Id)
                    }).OrderByDescending(x => x.CreatedDate).Take(10)
                    .AsDocumentQuery();

                    var pushSubs = new List<PushSubscriptionEntity>();
                    while (pushSubscriptionsQuery.HasMoreResults)
                    {
                        pushSubs.AddRange(
                            (await pushSubscriptionsQuery.ExecuteNextAsync<PushSubscriptionEntity>()).ToList()
                        );
                    }

                    if (pushSubs.Any())
                    {
                        // GO AHEAD
                        var json = JsonConvert.SerializeObject(new WebPushNotification
                        {
                            Notification = new WebPushInnerNotification
                            {
                                Title = foundTemplate.Title,
                                Body = foundTemplate.Message,
                                Vibrate = _config.GetValue<IEnumerable<int>>("Vibrate"),
                                Icon = _config["Icon"],
                                RequireInteraction = false,
                                Badge = _config["Badge"],
                                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                Data = new WebPushData
                                {
                                    Id = notificationEntity.Id, // TODO
                                    AbsoluteRouteUrl = dto.AbsoluteRouteUrl,
                                    Severity = dto.Severity,
                                    CreatedDate = DateTimeOffset.UtcNow,
                                    AdditionalData = dto.AdditionalData
                                }
                            }
                        }, new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        });

                        foreach (var pushSub in pushSubs)
                        {
                            try
                            {
                                var ps = JsonConvert.DeserializeObject<PushSubscriptionData>(pushSub.SubscriptionJson, new JsonSerializerSettings
                                {
                                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                                });
                                await Policy
                                .Handle<Exception>()
                                .WaitAndRetryAsync(new[] { TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1500) })
                                .ExecuteAsync(async () =>
                                {
                                    await _webPushClient.SendNotificationAsync(new WebPush.PushSubscription(ps.Endpoint, ps.P256Dh, ps.Auth),
                                    json, _vapidDetails);
                                });
                            }
                            catch (Exception)
                            {
                                var docUri = UriFactory.CreateDocumentUri("notifications", "PushSubscriptions", pushSub.Id);
                                await pushSubscriptionsClient.DeleteDocumentAsync(docUri, new RequestOptions
                                {
                                    PartitionKey = new PartitionKey(userDto.Id)
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}
