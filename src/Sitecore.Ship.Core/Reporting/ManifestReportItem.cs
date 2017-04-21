namespace Sitecore.Ship.Core.Reporting
{
    public class ManifestReportItem
    {
        public string FullPath { get; set; }
        public string UpdateType { get; set; }
        public string Id { get; set; }

        public override string ToString()
        {
            return "[" + UpdateType + "] " + FullPath + " " + Id;
        }
    }
}