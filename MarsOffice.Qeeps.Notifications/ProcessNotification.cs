using System.Net.Http;
using System.Threading.Tasks;
using MarsOffice.Qeeps.Notifications.Abstractions;
using MarsOffice.Qeeps.Notifications.Entities;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using SendGrid;

namespace MarsOffice.Qeeps.Notifications
{
    public class ProcessNotification
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _accessClient;
        private readonly HttpClient _client;
        private readonly ISendGridClient _sendGridClient;

        public ProcessNotification(IConfiguration config, IHttpClientFactory httpClientFactory, ISendGridClient sendGridClient)
        {
            _config = config;
            _accessClient = httpClientFactory.CreateClient("access");
            _client = httpClientFactory.CreateClient();
            _sendGridClient = sendGridClient;
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
            
            await Task.CompletedTask;
        }
    }
}
