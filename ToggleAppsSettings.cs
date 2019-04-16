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
	public class ToggleAppsSettingsRequest
	{
	}

	public static class ToggleAppsSettings
	{
		[FunctionName("ToggleAppsSettings")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.WithState<SetActiveRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				state.ActiveApp = null;

				state.IsAppsSettings = !state.IsAppsSettings;

				if (!state.IsAppsSettings)
					state.AppsNavState = null;

				return state;
			});
		}
	}
}
