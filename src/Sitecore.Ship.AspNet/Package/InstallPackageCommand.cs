using System;
using System.Net;
using System.Web;
using System.Web.Helpers;

using Sitecore.Ship.Core;
using Sitecore.Ship.Core.Contracts;
using Sitecore.Ship.Core.Domain;
using Sitecore.Ship.Core.Services;
using Sitecore.Ship.Infrastructure;
using Sitecore.Ship.Infrastructure.Configuration;
using Sitecore.Ship.Infrastructure.DataAccess;
using Sitecore.Ship.Infrastructure.Install;
using Sitecore.Ship.Infrastructure.Update;
using Sitecore.Ship.Infrastructure.Web;

namespace Sitecore.Ship.AspNet.Package
{
    public class InstallPackageCommand : CommandHandler
    {
        private readonly IPackageRepository _repository;
        private readonly IInstallationRecorder _installationRecorder;
        private readonly IPublishService _publishService;

        public InstallPackageCommand(IPackageRepository repository, IInstallationRecorder installationRecorder, IPublishService publishService)
        {
            _repository = repository;
            _installationRecorder = installationRecorder;
            _publishService = publishService;
        }

        public InstallPackageCommand() 
            : this(new PackageRepository(new UpdatePackageRunner(new PackageManifestReader())),
                   new InstallationRecorder(new PackageHistoryRepository(), new PackageInstallationConfigurationProvider().Settings),
                    new PublishService())
        {           
        }

        public override void HandleRequest(HttpContextBase context)
        {
            if (CanHandle(context))
            {
                try
                {
                    var package = GetRequest(context.Request);
                    var manifest = _repository.AddPackage(package);
                    _installationRecorder.RecordInstall(package.Path, DateTime.Now);

                    foreach (var entry in manifest.Entries)
                    {
                        if (entry.ID.HasValue)
                        {
                            _publishService.AddToPublishQueue(entry.ID.Value);
                        }
                    }

                    var json = Json.Encode(new { manifest.ManifestReport });

                    JsonResponse(json, HttpStatusCode.Created, context);

                    context.Response.AddHeader("Location", ShipServiceUrl.PackageLatestVersion);
                }
                catch (NotFoundException)
                {
                    context.Response.StatusCode = (int) HttpStatusCode.NotFound;
                }
            }
            else if (Successor != null)
            {
                Successor.HandleRequest(context);
            }
        }

        private static bool CanHandle(HttpContextBase context)
        {
            return context.Request.Url != null && 
                   context.Request.Url.PathAndQuery.EndsWith("/services/package/install", StringComparison.InvariantCultureIgnoreCase) &&
                   context.Request.HttpMethod == "POST" && context.Response.StatusCode != (int)HttpStatusCode.Unauthorized; ;
        }

        private static InstallPackage GetRequest(HttpRequestBase request)
        {
            return new InstallPackage
                       {
                           Path = request.Form["path"],
                           DisableIndexing = ParseBoolean(request.Form["DisableIndexing"]),
                           EnableSecurityInstall = ParseBoolean(request.Form["EnableSecurityInstall"])
                       };
        }

        private static bool ParseBoolean(string request)
        {
            bool result;

            Boolean.TryParse(request, out result);

            return result;
        }
    }
}