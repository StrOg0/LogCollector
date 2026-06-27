using LogCollectorApp.Interfaces;

namespace LogCollectorApp.Tests.Fakes;

internal sealed class FakeSshFileHandler : ISshFileHandler
{
    public Dictionary<string, List<string>> DirectoryFiles { get; } = new();
    public Dictionary<string, string> RemoteTextFiles { get; } = new();

    public HashSet<string> DirectoriesThatThrow { get; } = new();
    public HashSet<string> FilesThatThrowOnDownload { get; } = new();

    public List<string> RequestedDirectories { get; } = new();
    public List<string> DownloadedFiles { get; } = new();

    public bool IsDisposed { get; private set; }

    public Task<List<string>> GetFilesListAsync(
        string host,
        int port,
        string username,
        string password,
        string remoteDirectoryPath,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        RequestedDirectories.Add(remoteDirectoryPath);

        if (DirectoriesThatThrow.Contains(remoteDirectoryPath))
            throw new InvalidOperationException($"Каталог недоступен: {remoteDirectoryPath}");

        if (!DirectoryFiles.TryGetValue(remoteDirectoryPath, out List<string>? files))
            return Task.FromResult(new List<string>());

        return Task.FromResult(files.ToList());
    }

    public Task DownloadFileAsync(
        string host,
        int port,
        string username,
        string password,
        string remoteFilePath,
        string localTempDirectory,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        DownloadedFiles.Add(remoteFilePath);

        if (FilesThatThrowOnDownload.Contains(remoteFilePath))
            throw new InvalidOperationException($"Файл недоступен: {remoteFilePath}");

        if (!RemoteTextFiles.TryGetValue(remoteFilePath, out string? content))
            throw new FileNotFoundException($"Файл не найден в fake SSH: {remoteFilePath}");

        Directory.CreateDirectory(localTempDirectory);

        string localPath = Path.Combine(
            localTempDirectory,
            Path.GetFileName(remoteFilePath));

        File.WriteAllText(localPath, content);

        progress?.Report($"Fake download: {Path.GetFileName(remoteFilePath)}");

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(FakeSshFileHandler));
    }
}