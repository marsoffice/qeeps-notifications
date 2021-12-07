using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MarsOffice.Qeeps.Notifications.Abstractions;
using MarsOffice.Qeeps.Notifications.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly ISendGridClient _sendGridClient;
        private readonly WebPushClient _webPushClient;
        private readonly IValidator<RequestNotificationDto> _validator;
        private readonly IEnumerable<Template> _templates;
        private readonly VapidDetails _vapidDetails;
        private readonly IMapper _mapper;

        public ProcessNotification(IConfiguration config,
            ISendGridClient sendGridClient, IValidator<RequestNotificationDto> validator, IMapper mapper)
        {
            _config = config;
            _sendGridClient = sendGridClient;
            _validator = validator;
            _templates = _config.GetSection("Templates").Get<IEnumerable<Template>>();
            _vapidDetails = new VapidDetails(_config["VapidSubject"], _config["publicvapidkey"], _config["privatevapidkey"]);
            _webPushClient = new WebPushClient();
            _mapper = mapper;
        }

        [FunctionName("ProcessNotification")]
        public async Task Run(
            [ServiceBusTrigger(
                #if DEBUG
                "notifications-dev",
                #else
                "notifications",
                #endif
                Connection = "sbconnectionstring")] RequestNotificationDto dto,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "Notifications",
                ConnectionStringSetting = "cdbconnectionstring", PreferredLocations = "%location%")] DocumentClient notificationsClient,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "PushSubscriptions",
                ConnectionStringSetting = "cdbconnectionstring", PreferredLocations = "%location%")] DocumentClient pushSubscriptionsClient,
                ILogger logger
            )
        {
#if DEBUG
            var dbNotif = new Database
            {
                Id = "notifications"
            };
            await notificationsClient.CreateDatabaseIfNotExistsAsync(dbNotif);

            var colNotif = new DocumentCollection
            {
                Id = "Notifications",
                PartitionKey = new PartitionKeyDefinition
                {
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new System.Collections.ObjectModel.Collection<string>(new List<string>() { "/UserId" })
                }
            };
            await notificationsClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("notifications"), colNotif);

            var colPush = new DocumentCollection
            {
                Id = "PushSubscriptions",
                PartitionKey = new PartitionKeyDefinition
                {
                    Version = PartitionKeyDefinitionVersion.V2,
                    Paths = new System.Collections.ObjectModel.Collection<string>(new List<string>() { "/UserId" })
                }
            };
            await notificationsClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("notifications"), colPush);
#endif
            notificationsClient.ConnectionPolicy.UseMultipleWriteLocations = _config.GetValue<bool>("multimasterdatabase");
            pushSubscriptionsClient.ConnectionPolicy.UseMultipleWriteLocations = _config.GetValue<bool>("multimasterdatabase");

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
                        string value = null;
                        if (kvp.Key.ToLower().Contains("link"))
                        {
                            value = _config["UiUrl"] + kvp.Value;
                        }
                        else
                        {
                            value = kvp.Value;
                        }
                        t.Title = t.Title.Replace("{{" + kvp.Key + "}}", value);
                        t.Message = t.Message.Replace("{{" + kvp.Key + "}}", value);
                    }
                }
            }



            if (dto.Recipients == null || !dto.Recipients.Any())
            {
                return;
            }

            foreach (var foundUser in dto.Recipients)
            {
                var lang = "ro";
                if (!string.IsNullOrEmpty(foundUser.PreferredLanguage))
                {
                    lang = foundUser.PreferredLanguage;
                }
                else
                {
                    if (!string.IsNullOrEmpty(dto.PreferredLanguage))
                    {
                        lang = dto.PreferredLanguage;
                    }
                }

                var foundTemplate = foundTemplates.Single(x => x.Language == lang);

                if (dto.NotificationTypes.Contains(NotificationType.InApp))
                {
                    var notificationEntity = new NotificationEntity
                    {
                        UserId = foundUser.UserId,
                        AbsoluteRouteUrl = dto.AbsoluteRouteUrl,
                        CreatedDate = DateTime.UtcNow,
                        IsRead = false,
                        Message = foundTemplate.Message,
                        Title = foundTemplate.Title,
                        ReadDate = null,
                        Severity = dto.Severity
                    };
                    var uri = UriFactory.CreateDocumentCollectionUri("notifications", "Notifications");
                    var insertReply = await notificationsClient.CreateDocumentAsync(uri, notificationEntity, new RequestOptions
                    {
                        PartitionKey = new PartitionKey(foundUser.UserId)
                    });
                    notificationEntity.Id = insertReply.Resource.Id;

                    var notificationDto = _mapper.Map<NotificationDto>(notificationEntity);
                    notificationDto.Message = StripHtml(notificationDto.Message);

                    // SIGNALR
                    await SendSignalrNotification(foundUser.UserId, notificationDto);

                    // PUSH
                    var pushSubs = new List<PushSubscriptionEntity>();
                    try
                    {
                        var col = UriFactory.CreateDocumentCollectionUri("notifications", "PushSubscriptions");
                        var pushSubscriptionsQuery = pushSubscriptionsClient.CreateDocumentQuery<PushSubscriptionEntity>(col, new FeedOptions
                        {
                            PartitionKey = new PartitionKey(foundUser.UserId)
                        }).OrderByDescending(x => x.CreatedDate).Take(10)
                        .AsDocumentQuery();

                        while (pushSubscriptionsQuery.HasMoreResults)
                        {
                            pushSubs.AddRange(
                                (await pushSubscriptionsQuery.ExecuteNextAsync<PushSubscriptionEntity>()).ToList()
                            );
                        }
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    if (pushSubs.Any())
                    {
                        // GO AHEAD
                        var json = JsonConvert.SerializeObject(new WebPushNotification
                        {
                            Notification = new WebPushInnerNotification
                            {
                                Title = foundTemplate.Title,
                                Body = StripHtml(foundTemplate.Message),
                                Vibrate = _config.GetValue<IEnumerable<int>>("Vibrate"),
                                Icon = _config["Icon"],
                                RequireInteraction = false,
                                Badge = _config["Badge"],
                                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                                Data = new WebPushData
                                {
                                    Id = notificationEntity.Id,
                                    AbsoluteRouteUrl = dto.AbsoluteRouteUrl,
                                    Severity = dto.Severity,
                                    CreatedDate = DateTime.UtcNow,
                                    AdditionalData = dto.AdditionalData,
                                    OnActionClick = new WebPushDataOnActionClick
                                    {
                                        Default = new WebPushDataOnActionClickItem
                                        {
                                            Operation = "navigateLastFocusedOrOpen",
                                            Url = $"/from-notification?nid={notificationEntity.Id}&returnTo={WebUtility.UrlEncode(dto.AbsoluteRouteUrl ?? "/")}"
                                        }
                                    }
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
                                    await _webPushClient.SendNotificationAsync(new WebPush.PushSubscription(ps.Endpoint, ps.Keys.P256Dh, ps.Keys.Auth),
                                    json, _vapidDetails);
                                });
                            }
                            catch (Exception e)
                            {
                                logger.LogError(e, $"Push notification failed to be sent.");
                                var docUri = UriFactory.CreateDocumentUri("notifications", "PushSubscriptions", pushSub.Id);
                                await pushSubscriptionsClient.DeleteDocumentAsync(docUri, new RequestOptions
                                {
                                    PartitionKey = new PartitionKey(foundUser.UserId)
                                });
                            }
                        }
                    }
                }


                if (dto.NotificationTypes.Contains(NotificationType.Email) && !string.IsNullOrEmpty(foundUser.Email))
                {
                    try
                    {
                        var sgm = new SendGridMessage();
                        sgm.SetSubject(foundTemplate.Title);
                        sgm.SetFrom(_config["FromEmail"], _config["FromName"]);
                        sgm.AddTo(foundUser.Email);
                        sgm.AddContent(MimeType.Html, foundTemplate.Message);
                        await _sendGridClient.SendEmailAsync(sgm);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Email sending failed: " + foundUser.Email);
                    }
                }
            }
        }

        private async Task SendSignalrNotification(string userId, NotificationDto payload, CancellationToken? ct = null)
        {
            var connectionStrings = new List<string> {
                        _config["signalrconnectionstring"]
                    };
            var otherConnectionStrings = _config["othersignalrconnectionstrings"];
            if (!string.IsNullOrEmpty(otherConnectionStrings))
            {
                var otherConnectionStringsSplit = otherConnectionStrings.Split(",");
                connectionStrings.AddRange(otherConnectionStringsSplit);
            }
            foreach (var cs in connectionStrings)
            {
                using var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(option =>
                    {
                        option.ConnectionString = cs;
                    })
                    .BuildServiceManager();
                using var hubContext = await serviceManager.CreateHubContextAsync("main", ct == null ? CancellationToken.None : ct.Value);
                await hubContext.Clients.User(userId).SendAsync("notificationReceived", payload, ct == null ? CancellationToken.None : ct.Value);
            }
        }

        private static string StripHtml(string v)
        {
            if (string.IsNullOrEmpty(v))
            {
                return v;
            }
            return Regex.Replace(v, @"<[^>]*>", String.Empty);
        }
    }
}
