using LogCollector.Interfaces;
using Renci.SshNet;

namespace LogCollector.App.BLL;

public class SshFileHandler : ISshFileHandler
{
    private bool _disposed;

    public async Task DownloadFileAsync(string host, int port, string username, string password,
        string remoteFilePath, string localTempDirectory, IProgress<string> progress, CancellationToken ct)
    {
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Подключение к {host}...");

            using var client = new SftpClient(host, port, username, password);

            client.Connect();
            progress?.Report($"Скачивание {remoteFilePath}...");

            string localFilePath = Path.Combine(localTempDirectory, Path.GetFileName(remoteFilePath));

            using var localStream = File.Create(localFilePath);
            client.DownloadFile(remoteFilePath, localStream);

            client.Disconnect();
            progress?.Report($"Файл сохранен: {localFilePath}");
        }, ct);
    }

    public async Task<List<string>> GetFilesListAsync(string host, int port, string username, string password,
        string remoteDirectoryPath, CancellationToken ct)
    {
        ThrowIfDisposed();

        return await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();

            var files = client.ListDirectory(remoteDirectoryPath)
                .Where(f => !f.IsDirectory)
                .Select(f => f.FullName)
                .ToList();

            client.Disconnect();
            return files;
        }, ct);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SshFileHandler));
    }
}