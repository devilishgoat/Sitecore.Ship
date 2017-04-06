using System.Collections.Generic;
using Newtonsoft.Json;
using Sitecore.Ship.Core.Reporting;
using Sitecore.Update.Installer;

namespace Sitecore.Ship.Core.Domain
{
    public class PackageManifest
    {
        public PackageManifest()
        {
            Entries = new List<PackageManifestEntry>();
        }
        
        public List<PackageManifestEntry> Entries { get; private set; }

        
    
        public ManifestReport ManifestReport { get; set; }
    }
}