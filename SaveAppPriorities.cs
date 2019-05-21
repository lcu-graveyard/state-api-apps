using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.State.API.ForgePublic.Harness;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace LCU.State.API.Apps
{
	[Serializable]
	[DataContract]
	public class SaveAppPrioritiesRequest
	{
		[DataMember]
		public virtual List<AppPriorityModel> Apps { get; set; }
	}

	public static class SaveAppPriorities
	{
		[FunctionName("SaveAppPriorities")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.Manage<SaveAppPrioritiesRequest, LCUAppsState, ForgeAPIAppsStateHarness>(log, async (mgr, reqData) =>
            {
                return await mgr.SaveAppPriorities(reqData.Apps);
            });
		}
	}
}
