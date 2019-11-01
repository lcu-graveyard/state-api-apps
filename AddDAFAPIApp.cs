using LCU.Graphs;
using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.State.API.ForgePublic.Harness;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace LCU.State.API.Apps
{
	[Serializable]
	[DataContract]
	public class AddDAFAPIAppRequest
	{
		[DataMember]
		public virtual DAFAPIConfiguration API { get; set; }
	}

	public static class AddDAFAPIApp
	{
		[FunctionName("AddDAFAPIApp")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.Manage<AddDAFAPIAppRequest, LCUAppsState, ForgeAPIAppsStateHarness>(log, async (mgr, reqData) =>
            {
				log.LogInformation($"Add DAF API App: {reqData.API.ID}");

                return await mgr.AddDAFAPIApp(reqData.API);
            });
		}
	}
}
