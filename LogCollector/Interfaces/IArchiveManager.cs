using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogCollector.Interfaces
{
    public interface IArchiveManager
    {
        List<string> ExtractArchivesRecursively(string sourceArchivePath, string targetDirectory);

        string CreateResultArchive(string destinationArchivePath, Dictionary<string, string> logsByServer);
    }
}
