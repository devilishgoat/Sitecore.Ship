using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Microsoft.Design", 
    "CA2210:AssembliesShouldHaveValidStrongNames", 
    Justification = "Releases before Sitecore.Ship 1.0.0 will not have assemblies code signed")]