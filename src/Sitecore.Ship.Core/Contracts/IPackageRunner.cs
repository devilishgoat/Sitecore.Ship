﻿using Sitecore.Ship.Core.Domain;

namespace Sitecore.Ship.Core.Contracts
{
    public interface IPackageRunner
    {
        PackageManifest Execute(string packagePath, bool disableIndexing, bool enableSecurityInstall, bool analyzeOnly, bool summeryOnly, string version);
    }
}