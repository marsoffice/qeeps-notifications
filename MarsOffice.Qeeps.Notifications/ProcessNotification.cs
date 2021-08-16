using System.Threading.Tasks;
using MarsOffice.Qeeps.Notifications.Abstractions;
using MarsOffice.Qeeps.Notifications.Entities;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;

namespace MarsOffice.Qeeps.Notifications
{
    public class ProcessNotification
    {
        private readonly IConfiguration _config;

        public ProcessNotification(IConfiguration config)
        {
            _config = config;
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
