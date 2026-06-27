using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class SshFileHandlerIntegrationTests
{
    private SshFileHandler _handler = null!;
    private string _tempDir = null!;

    private string _host = null!;
    private int _port;
    private string _username = null!;
    private string _password = null!;
    private string _remoteDirectory = null!;
    private string _testFileName = null!;
    private string _remoteFilePath = null!;
    private string? _expectedText;

    [SetUp]
    public void SetUp()
    {
        _host = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_HOST");
        _username = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_USER");
        _password = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_PASSWORD");
        _remoteDirectory = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_DIR");

        string portText = GetRequiredEnvironmentVariable("LOGCOLLECTOR_SFTP_PORT");

        if (!int.TryParse(portText, out _port))
            Assert.Ignore("LOGCOLLECTOR_SFTP_PORT должен быть числом.");

        _testFileName = Environment.GetEnvironmentVariable("LOGCOLLECTOR_SFTP_FILE");

        if (string.IsNullOrWhiteSpace(_testFileName))
            Assert.Ignore("Интеграционный SFTP-тест пропущен: переменная LOGCOLLECTOR_SFTP_FILE не задана.");

        _expectedText = Environment.GetEnvironmentVariable("LOGCOLLECTOR_SFTP_EXPECTED_TEXT");

        _remoteFilePath = CombineRemotePath(_remoteDirectory, _testFileName);

        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _handler = new SshFileHandler();
    }

    [TearDown]
    public void TearDown()
    {
        _handler?.Dispose();

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task GetFilesListAsync_WhenRemoteDirectoryContainsTestFile_ReturnsRemoteFilePath()
    {
        List<string> files = await _handler.GetFilesListAsync(
            _host,
            _port,
            _username,
            _password,
            _remoteDirectory,
            CancellationToken.None);

        Assert.That(files, Does.Contain(_remoteFilePath));
    }

    [Test]
    public async Task DownloadFileAsync_WhenRemoteFileExists_CreatesLocalFile()
    {
        await _handler.DownloadFileAsync(
            _host,
            _port,
            _username,
            _password,
            _remoteFilePath,
            _tempDir,
            new Progress<string>(),
            CancellationToken.None);

        string localFilePath = Path.Combine(_tempDir, _testFileName);

        Assert.That(File.Exists(localFilePath), Is.True);
    }

    [Test]
    public async Task DownloadFileAsync_WhenRemoteFileExists_DownloadsNonEmptyContent()
    {
        await _handler.DownloadFileAsync(
            _host,
            _port,
            _username,
            _password,
            _remoteFilePath,
            _tempDir,
            new Progress<string>(),
            CancellationToken.None);

        string localFilePath = Path.Combine(_tempDir, _testFileName);
        string content = File.ReadAllText(localFilePath);

        Assert.That(content, Is.Not.Empty);

        if (!string.IsNullOrWhiteSpace(_expectedText))
            Assert.That(content, Does.Contain(_expectedText));
    }

    [Test]
    public async Task DownloadFileAsync_WhenProgressProvided_ReportsConnectionAndDownloadMessages()
    {
        var progress = new TestProgress();

        await _handler.DownloadFileAsync(
            _host,
            _port,
            _username,
            _password,
            _remoteFilePath,
            _tempDir,
            progress,
            CancellationToken.None);

        Assert.That(progress.Messages, Has.Some.Contains("Подключение"));
        Assert.That(progress.Messages, Has.Some.Contains("Скачивание"));
        Assert.That(progress.Messages, Has.Some.Contains(_testFileName));
    }

    [Test]
    public async Task GetFilesListAsync_WhenHostContainsProtocol_StillConnects()
    {
        string hostWithProtocol = $"sftp://{_host}";

        List<string> files = await _handler.GetFilesListAsync(
            hostWithProtocol,
            _port,
            _username,
            _password,
            _remoteDirectory,
            CancellationToken.None);

        Assert.That(files, Does.Contain(_remoteFilePath));
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
            Assert.Ignore($"Интеграционный SFTP-тест пропущен: переменная окружения {name} не задана.");

        return value;
    }

    private static string CombineRemotePath(string directory, string fileName)
    {
        return $"{directory.TrimEnd('/')}/{fileName.TrimStart('/')}";
    }

    private sealed class TestProgress : IProgress<string>
    {
        private readonly object _lock = new();

        public List<string> Messages { get; } = new();

        public void Report(string value)
        {
            lock (_lock)
            {
                Messages.Add(value);
            }
        }
    }
}