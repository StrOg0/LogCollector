namespace LogCollector.Interfaces
{
    public interface IArchiveManager
    {
        List<string> ExtractArchives(string sourceArchivePath, string targetDirectory);

        string CreateResultArchive(string destinationArchivePath, Dictionary<string, string> logsByServer);
    }
}
