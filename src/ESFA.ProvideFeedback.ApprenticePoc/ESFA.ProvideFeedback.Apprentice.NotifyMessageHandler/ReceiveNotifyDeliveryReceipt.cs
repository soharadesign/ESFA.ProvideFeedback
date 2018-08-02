using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ESFA.ProvideFeedback.Apprentice.NotifyMessageHandler
{
    public static partial class ReceiveNotifyDeliveryReceipt
    {
        [FunctionName("ReceiveNotifyDeliveryReceipt")]
        [return: Queue("sms-delivery-log")]
        public static ActionResult Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req,
            TraceWriter log,
            ExecutionContext context)
        {
            log.Info("ReceiveNotifyDeliveryReceipt trigger function processed a request.");

            string id = req.Query["id"];

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            id = id ?? data?.id;

            return data != null
                ? (ActionResult)new OkObjectResult(data)
                : new BadRequestObjectResult("Expecting a text message receipt payload. Ensure that the payload has an ID, reference, recipient, status and notification type");
        }
    }
}