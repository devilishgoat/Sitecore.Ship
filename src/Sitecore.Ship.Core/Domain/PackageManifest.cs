using System.Collections.Generic;
using Sitecore.Ship.Core.Reporting;

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