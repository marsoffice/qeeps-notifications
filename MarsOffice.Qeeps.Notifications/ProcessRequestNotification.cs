using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;

namespace MarsOffice.Qeeps.Notifications
{
    public class ProcessRequestNotification
    {

        public ProcessRequestNotification()
        {
        }

        [FunctionName("ProcessRequestNotification")]
        public async Task<IActionResult> Run(
            
            )
        {
            await Task.CompletedTask;
            return new OkResult();
        }
    }
}
