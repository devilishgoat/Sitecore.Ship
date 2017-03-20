using System.Collections.Generic;

namespace Sitecore.Ship.Core.Reporting
{
    public class ManifestReport
    {
        public bool AnalyzeOnly { get; set; }
        public bool CanDeleteItems { get; set; }
        public List<ManifestReportDataBase> Databases { get; set; }

        public string Error { get; set; }
        

        public ManifestReport SetError(string errorMsg)
        {
            this.Error = errorMsg;
            return this;
        }
    }
}