namespace Sitecore.Ship.Core.Domain
{
    public class PackageCommandsBase
    {
        public bool DisableIndexing { get; set; }

        /// <summary>
        /// Set to true to disable reporting of items contained in the package.
        /// </summary>
        public bool DisableManifest { get; set; }

        public bool EnableSecurityInstall { get; set; }

        public bool AnalyzeOnly { get; set; }

        public string Version { get; set; }

        public bool SummeryOnly { get; set; }
    }
}