using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sitecore.Ship.Core.Reporting
{
    public class ManifestReportDataBase
    {
        public string Database { get; set; }
        public List<ManifestReportItem> Items { get; set; }
    }
}
