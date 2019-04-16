using Fathym;
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
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace LCU.State.API.Apps
{
	[Serializable]
	[DataContract]
	public class SaveDAFAppsRequest
	{
		[DataMember]
		public virtual List<DAFApplicationConfiguration> DAFApps { get; set; }
	}

	public static class SaveDAFApps
	{
		[FunctionName("SaveDAFApps")]
		public static async Task<IActionResult> Run(
			[HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
			ILogger log)
		{
			return await req.WithState<SaveDAFAppsRequest, LCUAppsState>(log, async (details, reqData, state, stateMgr) =>
			{
				if (state.ActiveApp != null)
				{
					log.LogInformation($"Saving DAF Apps: {reqData.DAFApps?.ToJSON()}");

					var appGraph = req.LoadGraph<ApplicationGraph>();

					reqData.DAFApps.Each(da =>
					{
						da.ApplicationID = state.ActiveApp.ID;

						if (da.ID.IsEmpty() && da.Priority <= 0)
							da.Priority = reqData.DAFApps.Max(a => a.Priority) + 500; 

						var status = Status.Success;

						log.LogInformation($"Saving DAF App: {da.ToJSON()}");

						if (da != null && da.Metadata.ContainsKey("NPMPackage"))
							status = unpackView(req, da, details.EnterpriseAPIKey, log).Result;

						if (status)
						{
							var dafApp = appGraph.SaveDAFApplication(details.EnterpriseAPIKey, da).Result;
						}
					});

					state.ActiveDAFApps = await appGraph.GetDAFApplications(details.EnterpriseAPIKey, state.ActiveApp.ID);

					state.ActiveAppType = state.ActiveDAFApps.Any(da => da.Metadata.ContainsKey("APIRoot")) ? "API" : "View";
				}

				return state;
			});
		}

		private static async Task<Status> unpackView(HttpRequest req, DAFApplicationConfiguration dafApp, string entApiKey,
			ILogger log)
		{
			var viewApp = dafApp.JSONConvert<DAFViewConfiguration>();

			if (viewApp.PackageVersion != "dev-stream")
			{
				log.LogInformation($"Unpacking view: {viewApp.ToJSON()}");

				var entGraph = req.LoadGraph<EnterpriseGraph>();

				var ent = await entGraph.LoadByPrimaryAPIKey(entApiKey);

				var client = new HttpClient();

				var npmUnpackUrl = Environment.GetEnvironmentVariable("NPM-PUBLIC-URL");

				var npmUnpackCode = Environment.GetEnvironmentVariable("NPM-PUBLIC-CODE");

				var npmUnpack = $"{npmUnpackUrl}/api/npm-unpack?code={npmUnpackCode}&pkg={viewApp.NPMPackage}&version={viewApp.PackageVersion}&applicationId={dafApp.ApplicationID}&enterpriseId={ent.ID}";

				log.LogInformation($"Unpacking view at: {npmUnpack}");

				var response = await client.GetAsync(npmUnpack);

				object statusObj = await response.Content.ReadAsJSONAsync<dynamic>();

				var status = statusObj.JSONConvert<Status>();

				log.LogInformation($"View unpacked: {status.ToJSON()}");

				if (status)
					dafApp.Metadata["PackageVersion"] = status.Metadata["Version"];

				return status;
			}
			else
				return Status.Success.Clone("Success", new { PackageVersion = viewApp.PackageVersion });
		}
	}
}
