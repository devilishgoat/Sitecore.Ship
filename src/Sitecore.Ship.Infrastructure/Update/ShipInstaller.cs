using Sitecore.Update.Installer;
using Sitecore.Diagnostics;
using Sitecore.Install.Framework;
using Sitecore.Install.Zip;
using Sitecore.Update.Installer.Utils;

namespace Sitecore.Ship.Infrastructure.Update
{
    public class ShipInstaller : DiffInstaller
    {
        public ShipInstaller(UpgradeAction action) : base(action)
        {
        }

        public new void InstallSecurity(string path)
        {
            Assert.ArgumentNotNullOrEmpty(path, "path");
            this.InstallSecurity(path, new SimpleProcessingContext());
        }

        public new void InstallSecurity(string path, IProcessingContext context)
        {
            Assert.ArgumentNotNullOrEmpty(path, "path");
            Assert.ArgumentNotNull((object)context, "context");
            Log.Info("Installing security from package: " + path, (object)this);
            PackageReader packageReader = new PackageReader(path);
            AccountInstaller accountInstaller = new AccountInstaller();
            accountInstaller.Initialize(context);
            packageReader.Populate((ISink<PackageEntry>)accountInstaller);
            accountInstaller.Flush();
            accountInstaller.Finish();
        }
    }
}
