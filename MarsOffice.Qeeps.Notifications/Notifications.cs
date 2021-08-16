using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MarsOffice.Qeeps.Microfunction;
using MarsOffice.Qeeps.Notifications.Abstractions;
using MarsOffice.Qeeps.Notifications.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Qeeps.Notifications
{

    public class Notifications
    {
        private readonly IMapper _mapper;
        public Notifications(IMapper mapper)
        {
            _mapper = mapper;
        }

        [FunctionName("GetAllNotifications")]
        public async Task<IActionResult> GetAllNotifications(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/notifications")] HttpRequest req,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "Notifications",
                #if DEBUG
                CreateIfNotExists = true,
                PartitionKey = "UserId",
                #endif
                ConnectionStringSetting = "cdbconnectionstring")] DocumentClient client)
        {
            var principal = QeepsPrincipal.Parse(req);
            var uid = principal.FindFirstValue("id");

            int page = req.Query.ContainsKey("page") ? int.Parse(req.Query["page"].ToString()) : 1;
            int elementsPerPage = req.Query.ContainsKey("elementsPerPage") ? int.Parse(req.Query["elementsPerPage"].ToString()) : 50;

            var col = UriFactory.CreateDocumentCollectionUri("notifications", "Notifications");


            var total = await client.CreateDocumentQuery<NotificationEntity>(col, new FeedOptions
            {
                PartitionKey = new PartitionKey(uid)
            })
            .Where(x => x.UserId == uid)
            .CountAsync();

            var unread = await client.CreateDocumentQuery<NotificationEntity>(col, new FeedOptions
            {
                PartitionKey = new PartitionKey(uid)
            })
            .Where(x => x.UserId == uid && x.IsRead == false)
            .CountAsync();

            var resultsQuery = client.CreateDocumentQuery<NotificationEntity>(col, new FeedOptions
            {
                PartitionKey = new PartitionKey(uid)
            })
            .Where(x => x.UserId == uid)
            .OrderByDescending(x => x.CreatedDate)
            .Skip((page - 1) * elementsPerPage)
            .Take(elementsPerPage)
            .AsDocumentQuery();

            var results = new List<NotificationEntity>();

            while (resultsQuery.HasMoreResults)
            {
                var entities = (await resultsQuery.ExecuteNextAsync<NotificationEntity>()).ToList();
                results.AddRange(entities);
            }

            var reply = new NotificationsDto
            {
                Total = total,
                Unread = unread,
                Notifications = _mapper.Map<IEnumerable<NotificationDto>>(results)
            };

            return new JsonResult(reply, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
        }

        [FunctionName("MarkAsRead")]
        public async Task<IActionResult> MarkAsRead(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "api/notifications/{id}/markAsRead")] HttpRequest req,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "Notifications",
                #if DEBUG
                CreateIfNotExists = true,
                PartitionKey = "UserId",
                #endif
                ConnectionStringSetting = "cdbconnectionstring")] DocumentClient client)
        {
            var principal = QeepsPrincipal.Parse(req);
            var uid = principal.FindFirstValue("id");
            var col = UriFactory.CreateDocumentCollectionUri("notifications", "Notifications");
            var notificationId = req.RouteValues["id"].ToString();
            var updateData = new
            {
                Id = notificationId,
                IsRead = true,
                ReadDate = System.DateTimeOffset.UtcNow
            };
            await client.UpsertDocumentAsync(col, updateData, new RequestOptions
            {
                PartitionKey = new PartitionKey(uid)
            }, true);
            return new OkResult();
        }

        [FunctionName("MarkAllAsRead")]
        public async Task<IActionResult> MarkAllAsRead(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "api/notifications/markAllAsRead")] HttpRequest req,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "Notifications",
                #if DEBUG
                CreateIfNotExists = true,
                PartitionKey = "UserId",
                #endif
                ConnectionStringSetting = "cdbconnectionstring")] DocumentClient client)
        {
            var principal = QeepsPrincipal.Parse(req);
            var uid = principal.FindFirstValue("id");
            var col = UriFactory.CreateDocumentCollectionUri("notifications", "Notifications");

            var allUnreadNotificationsQuery = client.CreateDocumentQuery<NotificationEntity>(col, new FeedOptions
            {
                PartitionKey = new PartitionKey(uid)
            })
            .Where(x => x.IsRead == false && x.UserId == uid)
            .AsDocumentQuery();

            var tasks = new List<Task<ResourceResponse<Document>>>();
            while (allUnreadNotificationsQuery.HasMoreResults)
            {
                var reply = await allUnreadNotificationsQuery.ExecuteNextAsync<NotificationEntity>();
                foreach (var ne in reply)
                {
                    var updateData = new
                    {
                        ne.Id,
                        IsRead = true,
                        ReadDate = System.DateTimeOffset.UtcNow
                    };
                    tasks.Add(client.UpsertDocumentAsync(col, updateData, new RequestOptions
                    {
                        PartitionKey = new PartitionKey(uid)
                    }, true));
                }
            }
            
            await Task.WhenAll(tasks);

            return new OkResult();
        }
    }
}