using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sitecore.IO;
using Sitecore.SecurityModel;
using Sitecore.Ship.Core;
using Sitecore.Ship.Core.Contracts;
using Sitecore.Ship.Core.Domain;
using Sitecore.Ship.Infrastructure.Extensions;
using Sitecore.Update;
using Sitecore.Update.Installer;
using Sitecore.Update.Installer.Exceptions;
using Sitecore.Update.Installer.Installer.Utils;
using Sitecore.Update.Installer.Utils;
using Sitecore.Update.Metadata;
using Sitecore.Update.Utils;
using Sitecore.Update.Wizard;
using ILog = log4net.ILog;

namespace Sitecore.Ship.Infrastructure.Update
{
    public class UpdatePackageRunner : IPackageRunner
    {
        private readonly IPackageManifestRepository _manifestRepository;

        public UpdatePackageRunner(IPackageManifestRepository manifestRepository)
        {
            _manifestRepository = manifestRepository;
        }

        public static List<ContingencyEntry> Install(PackageInstallationInfo info, ILog installationProcessLogger, out string historyPath)
        {
            historyPath = (string)null;
            using (new SecurityDisabler())
            {
                bool hasPostAction;
                return new DiffInstaller(info.Action).InstallPackage(info.Path, info.Mode, info.ProcessingMode, installationProcessLogger, (IList<ContingencyEntry>)new List<ContingencyEntry>(), "rollbackPackage.rlb", out hasPostAction, ref historyPath);
            }
        }

        public PackageManifest Execute(string packagePath, bool disableIndexing, bool enableSecurityInstall)
        {
            if (!File.Exists(packagePath)) throw new NotFoundException();

            using (new ShutdownGuard())
            {
                if (disableIndexing)
                {
                    Sitecore.Configuration.Settings.Indexing.Enabled = false;
                }

                var installationInfo = GetInstallationInfo(packagePath);
                string historyPath = null;
                List<ContingencyEntry> entries = null;

                var logger = Sitecore.Diagnostics.LoggerFactory.GetLogger(this); // TODO abstractions
                try
                {
                    entries = UpdateHelper.Install(installationInfo, logger, out historyPath);

                    //using (new SecurityDisabler())
                    //{
                    //    bool flag;
                    //    DiffInstaller installer = new DiffInstaller(installationInfo.Action);
                    //    entries = installer.InstallPackage(installationInfo.Path, installationInfo.Mode, logger, new List<ContingencyEntry>(), "rollbackPackage.rlb", out flag, ref historyPath);
                    //}

                    string error = string.Empty;

                    logger.Info("Executing post installation actions.");

                    MetadataView metadata = PreviewMetadataWizardPage.GetMetadata(packagePath, out error);

                    if (string.IsNullOrEmpty(error))
                    {
                        ShipInstaller diffInstaller = new ShipInstaller(UpgradeAction.Upgrade);
                        using (new SecurityDisabler())
                        {
                            if (enableSecurityInstall)
                            {
                                diffInstaller.InstallSecurity(packagePath);
                            }
                            
                            diffInstaller.ExecutePostInstallationInstructions(packagePath, historyPath, installationInfo.Mode, metadata, logger, ref entries);
                        }
                    }
                    else
                    {
                        logger.Info("Post installation actions error.");
                        logger.Error(error);
                    }

                    logger.Info("Executing post installation actions finished.");

                    return _manifestRepository.GetManifest(packagePath);

                }
                catch (PostStepInstallerException exception)
                {
                    entries = exception.Entries;
                    historyPath = exception.HistoryPath;
                    throw;
                }
                finally
                {
                    if (disableIndexing)
                    {
                        Sitecore.Configuration.Settings.Indexing.Enabled = true;
                    }

                    try
                    {
                        SaveInstallationMessages(entries, historyPath);
                    }
                    catch (Exception)
                    {
                        logger.Error("Failed to record installation messages");
                        foreach (var entry in entries ?? Enumerable.Empty<ContingencyEntry>())
                        {
                            logger.Info(string.Format("Entry [{0}]-[{1}]-[{2}]-[{3}]-[{4}]-[{5}]-[{6}]-[{7}]-[{8}]-[{9}]-[{10}]-[{11}]",
                                entry.Action,
                                entry.Behavior,
                                entry.CommandKey,
                                entry.Database,
                                entry.Level,
                                entry.LongDescription,
                                entry.MessageGroup,
                                entry.MessageGroupDescription,
                                entry.MessageID,
                                entry.MessageType,
                                entry.Number,
                                entry.ShortDescription));
                        }
                        throw;
                    }
                }
            }
        }
        
        private PackageInstallationInfo GetInstallationInfo(string packagePath)
        {
            if (string.IsNullOrEmpty(packagePath))
            {
                throw new Exception("Package is not selected.");
            }

            var info = new PackageInstallationInfo
            {
                Mode = InstallMode.Install,
                Action = UpgradeAction.Upgrade,
                Path = packagePath
            };
            
            info.SetProcessingMode();
            return info;
        }

        private void SaveInstallationMessages(List<ContingencyEntry> entries, string historyPath)
        {
            string path = Path.Combine(historyPath, "messages.xml");

            FileUtil.EnsureFolder(path);

            using (FileStream fileStream = File.Create(path))
            {
                new XmlEntrySerializer().Serialize(entries, fileStream);
            }
        }
    }
}