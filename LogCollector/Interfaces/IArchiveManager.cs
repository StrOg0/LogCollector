using LogCollector.Models;

namespace LogCollector.Interfaces
{
    public interface IArchiveManager
    {
        List<string> ExtractArchives(string sourceArchivePath, string targetDirectory, IProgress<ArchiveProgressInfo> progress = null);

        string CreateResultArchive(string destinationArchivePath, IEnumerable<ProcessedLogInfo> processedLogs, IProgress<ArchiveProgressInfo> progress = null);
    }
}
