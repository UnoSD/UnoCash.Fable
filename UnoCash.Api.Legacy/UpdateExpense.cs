using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace UnoCash.Api
{
    public static class UpdateExpense
    {
        // Upsert and delete add?
        [FunctionName("UpdateExpense")]
        public static Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = null)]
            HttpRequest req,
            ILogger log) => throw new NotImplementedException();
    }
}
