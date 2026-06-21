namespace LogCollectorApp.Interfaces;

public interface ISshFileHandler : IDisposable
{
    Task DownloadFileAsync(
        string host,
        int port,
        string remoteFilePath,
        string localTempDirectory,
        IProgress<string> progress,
        CancellationToken cancellationToken);

    Task<List<string>> GetFilesListAsync(
        string host,
        int port,
        string remoteDirectoryPath,
        CancellationToken cancellationToken);
}