using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MarsOffice.Qeeps.Notifications
{
    public class Notifications
    {

        public Notifications()
        {
        }

        [FunctionName("GetUnreadNotifications")]
        public async Task<IActionResult> GetUnreadNotifications(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/notifications/unread")] HttpRequest req)
        {
            
            return new OkResult();
        }
    }
}
