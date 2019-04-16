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
	public class SetAppsNavStateRequest
	{
		[DataMember]
		public virtual string State { get; set; }
	}

	public static class SetAppsNavState
	{
		[FunctionName("SetAppsNavState")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.WithState<SetAppsNavStateRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				state.AppsNavState = reqData.State;

				if (state.AppsNavState == "Prioritizing")
				{
					state.AppPriorities = state.Apps.Select(app => new AppPriorityModel()
					{
						AppID = app.ID,
						IsDefault = false,
						Priority = app.Priority,
						Name = app.Name,
						Path = app.PathRegex.TrimEnd('*')
					}).ToList();

					state.DefaultApps.Each(app =>
					{
						if (!state.AppPriorities.Any(ap => ap.AppID == app.ID))
							state.AppPriorities.Add(new AppPriorityModel()
							{
								AppID = app.ID,
								IsDefault = true,
								Priority = app.Priority,
								Name = app.Name,
								Path = app.PathRegex.TrimEnd('*')
							});
					});

					state.AppPriorities = state.AppPriorities.OrderByDescending(ap => ap.Priority).ToList();
				}

				return state;
			});
		}
	}
}
