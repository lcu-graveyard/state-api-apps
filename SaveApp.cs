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
	public class SaveAppRequest
	{
		[DataMember]
		public virtual Application Application { get; set; }
	}

	public static class SaveApp
	{
		[FunctionName("SaveApp")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.WithState<SaveAppRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				var appGraph = req.LoadGraph<ApplicationGraph>();

				reqData.Application.EnterprisePrimaryAPIKey = details.EnterpriseAPIKey;

				if (reqData.Application.Hosts.IsNullOrEmpty())
					reqData.Application.Hosts = new List<string>();

				if (!reqData.Application.Hosts.Contains(details.Host))
					reqData.Application.Hosts.Add(details.Host);

				if (reqData.Application.ID.IsEmpty() && reqData.Application.Priority <= 0 && !state.Apps.IsNullOrEmpty())
					reqData.Application.Priority = state.Apps.First().Priority + 500;

				var app = await appGraph.Save(reqData.Application);

				state.Apps = await appGraph.ListApplications(details.EnterpriseAPIKey);

				state.ActiveApp = state.Apps.FirstOrDefault(a => a.ID == app.ID);

				if (state.ActiveApp != null)
				{
					state.ActiveDAFApps = await appGraph.GetDAFApplications(details.EnterpriseAPIKey, state.ActiveApp.ID);

					state.ActiveAppType = state.ActiveDAFApps.Any(da => da.Metadata.ContainsKey("APIRoot")) ? "API" : "View";
				}

				return state;
			});
		}
	}
}
