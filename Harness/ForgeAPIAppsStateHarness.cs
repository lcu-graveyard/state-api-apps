using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Fathym;
using Fathym.Design.Singleton;
using LCU.Graphs;
using LCU.Graphs.Registry.Enterprises;
using LCU.Graphs.Registry.Enterprises.Apps;
using LCU.Graphs.Registry.Enterprises.Identity;
using LCU.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace LCU.State.API.ForgePublic.Harness
{
    public class ForgeAPIAppsStateHarness : LCUStateHarness<LCUAppsState>
    {
        #region Fields
        protected readonly ApplicationGraph appGraph;
        
        #endregion

        #region Properties

        #endregion

        #region Constructors
        public ForgeAPIAppsStateHarness(HttpRequest req, ILogger log, LCUAppsState state)
            : base(req, log, state)
        {
            appGraph = req.LoadGraph<ApplicationGraph>(log);
        }
        #endregion

        #region API Methods
        public virtual async Task<LCUAppsState> AddDAFAPIApp(DAFAPIConfiguration api)
        {
            if (state.ActiveApp != null)
            {
                api.ApplicationID = state.ActiveApp.ID;

                if (api.ID.IsEmpty() && api.Priority <= 0)
                    api.Priority = state.ActiveDAFApps.Max(a => a.Priority) + 500;

                var app = await appGraph.SaveDAFApplication(details.EnterpriseAPIKey, api);

                state.ActiveDAFApps = await appGraph.GetDAFApplications(details.EnterpriseAPIKey, state.ActiveApp.ID);

                state.ActiveAppType = state.ActiveDAFApps.Any(da => da.Metadata.ContainsKey("APIRoot")) ? "API" : "View";
            }

            return state;
        }

        public virtual async Task<LCUAppsState> Refresh()
        {
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
        }

        public virtual async Task<LCUAppsState> RemoveDAFAPIApp(DAFAPIConfiguration api)
        {
            if (state.ActiveApp != null)
            {
                api.ApplicationID = state.ActiveApp.ID;

                var app = await appGraph.RemoveDAFApplication(details.EnterpriseAPIKey, api);

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
        }

        public virtual async Task<LCUAppsState> SaveApp(Application application)
        {
            application.EnterprisePrimaryAPIKey = details.EnterpriseAPIKey;

            if (application.Hosts.IsNullOrEmpty())
                application.Hosts = new List<string>();

            if (!application.Hosts.Contains(details.Host))
                application.Hosts.Add(details.Host);

            if (application.ID.IsEmpty() && application.Priority <= 0 && !state.Apps.IsNullOrEmpty())
                application.Priority = state.Apps.First().Priority + 500;

            var app = await appGraph.Save(application);

            state.Apps = await appGraph.ListApplications(details.EnterpriseAPIKey);

            state.ActiveApp = state.Apps.FirstOrDefault(a => a.ID == app.ID);

            if (state.ActiveApp != null)
            {
                state.ActiveDAFApps = await appGraph.GetDAFApplications(details.EnterpriseAPIKey, state.ActiveApp.ID);

                state.ActiveAppType = state.ActiveDAFApps.Any(da => da.Metadata.ContainsKey("APIRoot")) ? "API" : "View";
            }

            return state;
        }

        public virtual async Task<LCUAppsState> SaveAppPriorities(List<AppPriorityModel> applications)
        {
            state.Apps = await appGraph.ListApplications(details.EnterpriseAPIKey);

            applications.Reverse();

            var defaultGroups = new Dictionary<int, List<AppPriorityModel>>();

            var nextDefaultPriority = 0;

            applications.Each(
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
        }

        public virtual async Task<LCUAppsState> SaveDAFApps(List<DAFApplicationConfiguration> dafApps)
        {
            if (state.ActiveApp != null)
            {
                log.LogInformation($"Saving DAF Apps: {dafApps?.ToJSON()}");

                dafApps.Each(da =>
                {
                    da.ApplicationID = state.ActiveApp.ID;

                    if (da.ID.IsEmpty() && da.Priority <= 0)
                        da.Priority = dafApps.Max(a => a.Priority) + 500;

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
        }

        public virtual async Task<LCUAppsState> SetActive(Guid? applicationID)
        {
            state.ActiveApp = state.Apps.FirstOrDefault(a => a.ID == applicationID);

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
        }

        public virtual async Task<LCUAppsState> SetActiveAppType(string type)
        {
            state.ActiveAppType = type;

            return state;
        }

        public virtual async Task<LCUAppsState> SetAppsNavState(string newState)
        {
            state.AppsNavState = newState;

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
        }

        public virtual async Task<LCUAppsState> SetDefaultApps(bool isDefault)
        {
            if (isDefault && !state.DefaultAppsEnabled)
            {
                await appGraph.CreateDefaultApps(details.EnterpriseAPIKey);

                state.DefaultApps = await appGraph.LoadDefaultApplications(details.EnterpriseAPIKey);

                state.DefaultAppsEnabled = await appGraph.HasDefaultApps(details.EnterpriseAPIKey);
            }
            else if (!isDefault)
            {
                log.LogInformation("Disabling Default Apps is not currently supported...");
            }

            return state;
        }

        public virtual async Task<LCUAppsState> ToggleAppAsDefault(Guid appID, bool isAdd)
        {
            if (isAdd)
                await appGraph.AddDefaultApp(details.EnterpriseAPIKey, appID);
            else
                await appGraph.RemoveDefaultApp(details.EnterpriseAPIKey, appID);

            state.DefaultApps = await appGraph.LoadDefaultApplications(details.EnterpriseAPIKey);

            return state;
        }

        public virtual async Task<LCUAppsState> ToggleAppsSettings()
        {
            state.ActiveApp = null;

            state.IsAppsSettings = !state.IsAppsSettings;

            if (!state.IsAppsSettings)
                state.AppsNavState = null;

            return state;
        }
        #endregion

        #region Helpers
        private static async Task<Status> unpackView(HttpRequest req, DAFApplicationConfiguration dafApp, string entApiKey,
            ILogger log)
        {
            var viewApp = dafApp.JSONConvert<DAFViewConfiguration>();

            if (viewApp.PackageVersion != "dev-stream")
            {
                log.LogInformation($"Unpacking view: {viewApp.ToJSON()}");

                var entGraph = req.LoadGraph<EnterpriseGraph>(log);

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

        #endregion
    }
}