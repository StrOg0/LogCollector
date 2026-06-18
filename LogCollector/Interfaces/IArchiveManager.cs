using LogCollector.Models;

namespace LogCollector.Interfaces
{
    public interface IArchiveManager
    {
        List<string> ExtractArchives(string sourceArchivePath, string targetDirectory, IProgress<ArchiveProgressInfo> progress = null);

        string CreateResultArchive(string destinationArchivePath, Dictionary<string, string> logsByServer, IProgress<ArchiveProgressInfo> progress = null);
    }
}
