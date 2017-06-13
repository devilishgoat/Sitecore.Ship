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

        [JsonIgnore]
        public List<ContingencyEntry> SummeryEntries { get; private set; }


        [JsonProperty(Order = 4)]
        public List<NoticeEntry> NoticeEntries
        {
            get
            {
                if (noticeEntries == null || noticeEntries.Count != SummeryEntries.Count)
                {
                    noticeEntries = new List<NoticeEntry>();
                    SummeryEntries.ForEach(sourceEntry => noticeEntries.Add(new NoticeEntry(sourceEntry)));
                }
                return noticeEntries;
            }
        }

        private List<NoticeEntry> noticeEntries = null;


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

    public class NoticeEntry
    {
        public string Level { get; set; }
        public string Behaviour { get; set; }
        public string Database { get; set; }
        public string Description { get; set; }

        public NoticeEntry(ContingencyEntry sourceEntry)
        {
            this.Level = sourceEntry.Level.ToString();
            this.Behaviour = sourceEntry.Behavior.ToString();
            this.Database = sourceEntry.Database;
            this.Description = sourceEntry.LongDescription;
        }
    }
}