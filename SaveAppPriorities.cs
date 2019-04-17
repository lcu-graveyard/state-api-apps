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
			return await req.WithState<SaveAppPrioritiesRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				var appGraph = req.LoadGraph<ApplicationGraph>(log);

				state.Apps = await appGraph.ListApplications(details.EnterpriseAPIKey);

				reqData.Apps.Reverse();

				var defaultGroups = new Dictionary<int, List<AppPriorityModel>>();

				var nextDefaultPriority = 0;

				reqData.Apps.Each(
					(app) =>
					{
						if (app.IsDefault)
						{
							nextDefaultPriority = app.Priority;

							defaultGroups[nextDefaultPriority] = new List<AppPriorityModel>();
						}
						else
						{
							if (!defaultGroups.ContainsKey(nextDefaultPriority))
								defaultGroups[nextDefaultPriority] = new List<AppPriorityModel>();

							defaultGroups[nextDefaultPriority].Add(app);
						}
					});

				var groupIndex = 0;

				var saveApps = new List<Application>();

				defaultGroups.Each(dg =>
				{
					var minimum = dg.Key;

					var maximum = -1;

					if (groupIndex < defaultGroups.Count - 1)
						maximum = defaultGroups.Keys.ElementAt(groupIndex + 1);

					var groupSize = dg.Value.Count;

					var step = maximum <= 0 ? 5000 : (int)Math.Floor((decimal)(maximum - minimum) / groupSize);

					var lastPriority = minimum;

					saveApps.AddRange(dg.Value.Select(v =>
					{
						var app = state.Apps.FirstOrDefault(a => a.ID == v.AppID);

						app.Priority = lastPriority = lastPriority + step;

						return app;
					}));

					groupIndex++;
				});

				var appSaves = saveApps.Select(sa => appGraph.Save(sa)).ToList();

				var apps = await appSaves.WhenAll();

				state.Apps = await appGraph.ListApplications(details.EnterpriseAPIKey);

				state.AppsNavState = null;

				return state;
			});
		}
	}
}
