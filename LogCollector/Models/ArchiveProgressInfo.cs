using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogCollector.Models
{
    public class ArchiveProgressInfo
    {
        public string Stage { get; set; }

        public string Message { get; set; }

        public int Percent { get; set; } = -1;
    }
}
