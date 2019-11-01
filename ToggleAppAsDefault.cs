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
using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.Graphs.Registry.Enterprises;
using LCU.State.API.ForgePublic.Harness;

namespace LCU.State.API.Apps
{
	[Serializable]
	[DataContract]
	public class ToggleAppAsDefaultRequest
	{
		[DataMember]
		public virtual Guid AppID { get; set; }

		[DataMember]
		public virtual bool IsAdd { get; set; }
	}

	public static class ToggleAppAsDefault
    {
        [FunctionName("ToggleAppAsDefault")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
		{
			return await req.Manage<ToggleAppAsDefaultRequest, LCUAppsState, ForgeAPIAppsStateHarness>(log, async (mgr, reqData) =>
            {
				log.LogInformation($"Toggling Apps As Default: {reqData.AppID} {reqData.IsAdd}");

                return await mgr.ToggleAppAsDefault(reqData.AppID, reqData.IsAdd);
            });
		}
    }
}
