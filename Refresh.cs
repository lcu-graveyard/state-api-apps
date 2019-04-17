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
using System.Threading.Tasks;

namespace LCU.State.API.Apps
{
	public static class Refresh
	{
		[FunctionName("Refresh")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.WithState<dynamic, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				var appGraph = req.LoadGraph<ApplicationGraph>(log);



				new[] {
					Task.Run(async () => {
						state.Apps = await appGraph.ListApplications(details.EnterpriseAPIKey);
					}),
					Task.Run(async () => {
						state.DefaultApps = await appGraph.LoadDefaultApplications(details.EnterpriseAPIKey);
					}),
					Task.Run(async () => {
						state.DefaultAppsEnabled = await appGraph.HasDefaultApps(details.EnterpriseAPIKey);
					}),
					Task.Run(async () => {
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
					})
				}.WhenAll();

				state.IsAppsSettings = false;

				state.AppsNavState = null;

				return state;
			});
		}
	}
}
