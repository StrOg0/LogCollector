namespace LogCollector.Interfaces;

public interface ISshFileHandler : IDisposable
{
    Task DownloadFileAsync(
        string host,
        int port,
        string username,
        string password,
        string remoteFilePath,
        string localTempDirectory,
        IProgress<string> progress,
        CancellationToken cancellationToken);

    Task<List<string>> GetFilesListAsync(
        string host,
        int port,
        string username,
        string password,
        string remoteDirectoryPath,
        CancellationToken cancellationToken);
}