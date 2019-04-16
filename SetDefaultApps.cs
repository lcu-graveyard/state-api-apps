using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs.Registry.Enterprises.Apps;
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
	[Serializable]
	[DataContract]
	public class SetDefaultAppsRequest
	{
		[DataMember]
		public virtual bool State { get; set; }
	}

	public static class SetDefaultApps
	{
		[FunctionName("SetDefaultApps")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.WithState<SetDefaultAppsRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				var appGraph = req.LoadGraph<ApplicationGraph>();

				if (reqData.State && !state.DefaultAppsEnabled)
				{
					await appGraph.CreateDefaultApps(details.EnterpriseAPIKey);

					state.DefaultApps = await appGraph.LoadDefaultApplications(details.EnterpriseAPIKey);

					state.DefaultAppsEnabled = await appGraph.HasDefaultApps(details.EnterpriseAPIKey);
				}
				else if (!reqData.State)
				{
					log.LogInformation("Disabling Default Apps is not currently supported...");
				}

				return state;
			});
		}
	}
}
