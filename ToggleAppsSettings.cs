using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.State.API.ForgePublic.Harness;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace LCU.State.API.Apps
{
	public static class ToggleAppsSettings
	{
		[FunctionName("ToggleAppsSettings")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.Manage<dynamic, LCUAppsState, ForgeAPIAppsStateHarness>(log, async (mgr, reqData) =>
            {
                return await mgr.ToggleAppsSettings();
            });
		}
	}
}
