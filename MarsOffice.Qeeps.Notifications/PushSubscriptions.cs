using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MarsOffice.Qeeps.Notifications
{
    public class PushSubscriptions
    {

        public PushSubscriptions()
        {
        }

        [FunctionName("AddPushSubscription")]
        public async Task<IActionResult> AddPushSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/notifications/pushSubscriptions")] HttpRequest req)
        {
            
            return new OkResult();
        }

        [FunctionName("DeletePushSubscription")]
        public async Task<IActionResult> DeletePushSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/notifications/pushSubscriptions/delete")] HttpRequest req)
        {
            
            return new OkResult();
        }
    }
}
