using LCU.Graphs;
using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs.Registry.Enterprises.Apps;
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
			return await req.WithState<AddDAFAPIAppRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				if (state.ActiveApp != null)
				{
					var appGraph = req.LoadGraph<ApplicationGraph>();

					reqData.API.ApplicationID = state.ActiveApp.ID;

					if (reqData.API.ID.IsEmpty() && reqData.API.Priority <= 0)
						reqData.API.Priority = state.ActiveDAFApps.Max(a => a.Priority) + 500;

					var app = await appGraph.SaveDAFApplication(details.EnterpriseAPIKey, reqData.API);

					state.ActiveDAFApps = await appGraph.GetDAFApplications(details.EnterpriseAPIKey, state.ActiveApp.ID);

					state.ActiveAppType = state.ActiveDAFApps.Any(da => da.Metadata.ContainsKey("APIRoot")) ? "API" : "View";
				}

				return state;
			});
		}
	}
}
