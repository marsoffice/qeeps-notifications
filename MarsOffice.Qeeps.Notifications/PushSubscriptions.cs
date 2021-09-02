using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Qeeps.Notifications
{
    public class PushSubscriptions
    {
        private readonly IMapper _mapper;

        public PushSubscriptions(IMapper mapper)
        {
            _mapper = mapper;
        }

        [FunctionName("AddPushSubscription")]
        public async Task<IActionResult> AddPushSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/notifications/pushSubscriptions/add")] HttpRequest req,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "PushSubscriptions",
                #if DEBUG
                CreateIfNotExists = true,
                PartitionKey = "/UserId",
                #endif
                ConnectionStringSetting = "cdbconnectionstring")] IAsyncCollector<PushSubscriptionEntity> pushSubscriptionsOut,
            ILogger log)
        {
            try
            {
                var principal = QeepsPrincipal.Parse(req);
                using var streamReader = new StreamReader(req.Body);
                var payload = JsonConvert.DeserializeObject<PushSubscriptionDto>(
                    await streamReader.ReadToEndAsync(),
                    new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }
                );
                var entity = _mapper.Map<PushSubscriptionEntity>(payload);
                entity.UserId = principal.FindFirstValue("id");
                await pushSubscriptionsOut.AddAsync(entity);
                return new OkResult();
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("DeletePushSubscription")]
        public async Task<IActionResult> DeletePushSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/notifications/pushSubscriptions/delete")] HttpRequest req,
            [CosmosDB(
                databaseName: "notifications",
                collectionName: "PushSubscriptions",
                ConnectionStringSetting = "cdbconnectionstring")] DocumentClient client,
            ILogger log)
        {
            try
            {
#if DEBUG
                var dbNotif = new Database
                {
                    Id = "notifications"
                };
                await client.CreateDatabaseIfNotExistsAsync(dbNotif);

                var colPush = new DocumentCollection
                {
                    Id = "PushSubscriptions",
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Version = PartitionKeyDefinitionVersion.V1,
                        Paths = new System.Collections.ObjectModel.Collection<string>(new List<string>() { "/UserId" })
                    }
                };
                await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("notifications"), colPush);
#endif
                var principal = QeepsPrincipal.Parse(req);
                using var streamReader = new StreamReader(req.Body);
                var payload = JsonConvert.DeserializeObject<PushSubscriptionDto>(
                    await streamReader.ReadToEndAsync(),
                    new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    }
                );
                var col = UriFactory.CreateDocumentCollectionUri("notifications", "PushSubscriptions");
                var uid = principal.FindFirstValue("id");
                var deleteTasks = new List<Task<ResourceResponse<Document>>>();
                var query = client.CreateDocumentQuery<PushSubscriptionEntity>(col, new FeedOptions
                {
                    PartitionKey = new PartitionKey(uid)
                })
                    .Where(x => x.UserId == uid && x.SubscriptionJson == payload.SubscriptionJson)
                    .AsDocumentQuery();
                while (query.HasMoreResults)
                {
                    var results = await query.ExecuteNextAsync<PushSubscriptionEntity>();
                    var docUris = results.Select(x => UriFactory.CreateDocumentUri("notifications", "PushSubscriptions", x.Id)).ToList();
                    deleteTasks.AddRange(
                        docUris.Select(x => client.DeleteDocumentAsync(x, new RequestOptions
                        {
                            PartitionKey = new PartitionKey(uid)
                        })).ToList()
                    );
                }
                await Task.WhenAll(deleteTasks);

                return new OkResult();
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }
    }
}
