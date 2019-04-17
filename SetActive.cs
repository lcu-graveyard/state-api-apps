using LCU.Graphs;
using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs.Registry.Enterprises.Apps;
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
	public class SetActiveRequest
	{
		[DataMember]
		public virtual Guid? ApplicationID { get; set; }
	}

	public static class SetActive
	{
		[FunctionName("SetActive")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.WithState<SetActiveRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				var appGraph = req.LoadGraph<ApplicationGraph>(log);

				state.ActiveApp = state.Apps.FirstOrDefault(a => a.ID == reqData.ApplicationID);

				if (state.ActiveApp != null)
				{
					state.ActiveDAFApps = await appGraph.GetDAFApplications(details.EnterpriseAPIKey, state.ActiveApp.ID);

					if (state.ActiveDAFApps.IsNullOrEmpty())
						state.ActiveDAFApps = new List<DAFApplicationConfiguration>()
						{
							new DAFApplicationConfiguration()
						};

					if (state.ActiveDAFApps.Any(da => !da.ID.IsEmpty()))
						state.ActiveAppType = state.ActiveDAFApps.Any(da => da.Metadata.ContainsKey("APIRoot")) ? "API" : "View";
					else
						state.ActiveAppType = null;
				}
				else
				{
					state.ActiveAppType = null;

					state.ActiveDAFApps = new List<DAFApplicationConfiguration>();
				}

				state.IsAppsSettings = false;

				return state;
			});
		}
	}
}
