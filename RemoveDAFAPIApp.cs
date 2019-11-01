using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.Graphs;
using System.Linq;
using System.Collections.Generic;
using LCU.State.API.ForgePublic.Harness;

namespace LCU.State.API.Apps
{
	[Serializable]
	[DataContract]
	public class RemoveDAFAPIAppRequest
	{
		[DataMember]
		public virtual DAFAPIConfiguration API { get; set; }
	}

	public static class RemoveDAFAPIApp
    {
        [FunctionName("RemoveDAFAPIApp")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
		{
			return await req.Manage<RemoveDAFAPIAppRequest, LCUAppsState, ForgeAPIAppsStateHarness>(log, async (mgr, reqData) =>
            {
				log.LogInformation($"Removing DAF API: {reqData.API.ID}");
                return await mgr.RemoveDAFAPIApp(reqData.API);
            });
		}
    }
}
