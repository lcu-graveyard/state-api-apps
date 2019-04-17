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
			return await req.WithState<RemoveDAFAPIAppRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				if (state.ActiveApp != null)
				{
					var appGraph = req.LoadGraph<ApplicationGraph>(log);

					reqData.API.ApplicationID = state.ActiveApp.ID;

					var app = await appGraph.RemoveDAFApplication(details.EnterpriseAPIKey, reqData.API);

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

				return state;
			});
		}
    }
}
