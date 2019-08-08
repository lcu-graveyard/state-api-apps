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
using LCU.Presentation.Personas.Applications;
using LCU.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace LCU.State.API.ForgePublic.Harness
{
    public class ForgeAPIAppsStateHarness : LCUStateHarness<LCUAppsState>
    {
        #region Fields
        protected readonly ApplicationManagerClient appMgr;

        #endregion

        #region Properties

        #endregion

        #region Constructors
        public ForgeAPIAppsStateHarness(HttpRequest req, ILogger logger, LCUAppsState state)
            : base(req, logger, state)
        {
            appMgr = req.ResolveClient<ApplicationManagerClient>(logger);
        }
        #endregion

        #region API Methods
        public virtual async Task<LCUAppsState> AddDAFAPIApp(DAFAPIConfiguration api)
        {
            if (state.ActiveApp != null)
            {
                var saved = await appDev.SaveDAFAPI(api, state.ActiveApp.ID, details.EnterpriseAPIKey);

                await LoadDAFApps();
            }

            return state;
        }

        public virtual async Task<LCUAppsState> LoadApps()
        {
            var apps = await appMgr.ListApplications(details.EnterpriseAPIKey);

            state.Apps = apps.Model;

            return state;
        }

        public virtual async Task<LCUAppsState> LoadDefaultApps()
        {
            var apps = await appMgr.ListDefaultApplications(details.EnterpriseAPIKey);

            state.DefaultApps = apps.Model;

            var defApps = await appMgr.HasDefaultApplications(details.EnterpriseAPIKey);

            state.DefaultAppsEnabled = defApps.Status;

            return state;
        }

        public virtual async Task<LCUAppsState> LoadDAFApps(List<DAFApplicationConfiguration> dafApps = null)
        {
            if (dafApps.IsNullOrEmpty())
            {
                var dafAppsResponse = await appMgr.ListDAFApplications(details.EnterpriseAPIKey, state.ActiveApp.ID);

                state.ActiveDAFApps = dafAppsResponse.Model;
            }
            else
                state.ActiveDAFApps = dafApps;

            if (state.ActiveDAFApps.IsNullOrEmpty())
                state.ActiveDAFApps = new List<DAFApplicationConfiguration>()
                {
                    new DAFApplicationConfiguration()
                };

            if (state.ActiveDAFApps.Any(da => !da.ID.IsEmpty()))
                state.ActiveAppType = state.ActiveDAFApps.Any(da => da.Metadata.ContainsKey("APIRoot")) ? "API" : "View";
            else
                state.ActiveAppType = null;

            return state;
        }

        public virtual async Task<LCUAppsState> Refresh()
        {
            new[] {
                Task.Run(async () => {
                    await LoadApps();
                }),
                Task.Run(async () => {
                    await LoadDefaultApps();
                }),
                Task.Run(async () => {
                    var apps = await appMgr.HasDefaultApplications(details.EnterpriseAPIKey);

                    state.DefaultAppsEnabled = apps.Status;
                }),
                Task.Run(async () => {
                    if (state.ActiveApp != null)
                        await LoadDAFApps();
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
                var removed = await appDev.RemoveDAFApp(state.ActiveApp.ID, api.ID, details.EnterpriseAPIKey);

                await LoadDAFApps();
            }

            return state;
        }

        public virtual async Task<LCUAppsState> SaveApp(Application application)
        {
            var app = await appDev.SaveApp(application, details.Host, String.Empty, details.EnterpriseAPIKey);

            await LoadApps();

            await SetActive(app.Model.ID);

            state.ActiveApp = state.Apps.FirstOrDefault(a => a.ID == app.Model.ID);

            if (state.ActiveApp != null)
                await LoadDAFApps();

            return state;
        }

        public virtual async Task<LCUAppsState> SaveAppPriorities(List<AppPriorityModel> applications)
        {
            applications.Reverse();

            var apps = await appDev.SaveAppPriorities(applications, details.EnterpriseAPIKey);

            await LoadApps();

            state.AppsNavState = null;

            return state;
        }

        public virtual async Task<LCUAppsState> SaveDAFApps(List<DAFApplicationConfiguration> dafApps)
        {
            if (state.ActiveApp != null)
            {
                var dafAppsResponse = await appDev.SaveDAFApps(dafApps, state.ActiveApp.ID, details.EnterpriseAPIKey);

                await LoadDAFApps(dafAppsResponse.Model);
            }

            return state;
        }

        public virtual async Task<LCUAppsState> SetActive(Guid? applicationID)
        {
            var activeApp = state.Apps.FirstOrDefault(a => a.ID == applicationID);

            if (activeApp != null && activeApp.ID != state.ActiveApp.ID)
            {
                state.ActiveApp = activeApp;

                await LoadDAFApps();
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
                await appDev.EnsureDefaultApps(details.EnterpriseAPIKey);

                await LoadDefaultApps();
            }
            else if (!isDefault)
            {
                logger.LogInformation("Disabling Default Apps is not currently supported...");
            }

            return state;
        }

        public virtual async Task<LCUAppsState> ToggleAppAsDefault(Guid appId, bool isAdd)
        {
            await appDev.ToggleAppAsDefault(details.EnterpriseAPIKey, appId);

            await LoadDefaultApps();

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
        #endregion
    }
}