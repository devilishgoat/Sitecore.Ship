using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Sitecore.Update.Installer;

namespace Sitecore.Ship.Core.Reporting
{
    public class ManifestReport
    {
        public ManifestReport()
        {
            this.SummeryEntries = new List<ContingencyEntry>();
        }


        [JsonProperty(Order = 1)]
        public string Error { get; set; }

        [JsonProperty(Order = 2)]
        public bool AnalyzeOnly { get; set; }

        [JsonProperty(Order = 3)]
        public bool CanDeleteItems { get; set; }

        [JsonProperty(Order = 4)]
        public List<ContingencyEntry> SummeryEntries { get; private set; }

        [JsonProperty(Order = 5)]
        public List<ManifestReportDataBase> Databases { get; set; }

        public bool ErrorOccured
        {
            get
            {
                return (!string.IsNullOrEmpty(Error)) ||
                       (SummeryEntries.Any(entry => entry.Level == ContingencyLevel.Error));
            }
        }

        public bool WarningOccured
        {
            get
            {
                return (SummeryEntries.Any(entry => entry.Level == ContingencyLevel.Warning));
            }
        }

        public ManifestReport SetError(string errorMsg)
        {
            this.Error = errorMsg;
            return this;
        }
    }
}