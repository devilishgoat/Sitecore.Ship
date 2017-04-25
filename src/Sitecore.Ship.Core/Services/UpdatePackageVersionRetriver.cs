using System.Linq;
using Sitecore.Globalization;
using Sitecore.SecurityModel;

namespace Sitecore.Ship.Core.Services
{
    public class UpdatePackageVersionRetriver
    {
        public static string GetUpdatePackageVersion(string packageName)
        {
            Sitecore.Data.Database coreDB = Sitecore.Configuration.Factory.GetDatabase("core");

            using (new SecurityDisabler())
            {
                var normalizedPackageVersion = packageName.Replace(".", "");
                var installItem = coreDB.GetItem("/sitecore/system/Packages/Installation history/"+ normalizedPackageVersion, Language.Parse("en-GB"));
                if (installItem == null|| installItem.Children.Count==0)
                {
                    return string.Empty;
                }

                installItem = installItem.Children.OrderBy(i => i.Name).Last();
                return installItem.Fields["Package version"].Value;
            }
        }
    }
}