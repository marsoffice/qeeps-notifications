using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MarsOffice.Qeeps.Notifications
{
    public class Healthcheck
    {

        public Healthcheck()
        {
        }

        [FunctionName("Healthcheck")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "healthcheck")] HttpRequest req)
        {
            await Task.CompletedTask;
            return new OkResult();
        }
    }
}
