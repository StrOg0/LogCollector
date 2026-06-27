using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class SshFileHandlerIntegrationTests
{
    private const string TestFileName = "integration-test.log";
    private const string ExpectedContent = "SFTP integration test";

    private SshFileHandler _handler = null!;
    private string _tempDir = null!;

    private string _host = null!;
    private int _port;
    private string _username = null!;
    private string _password = null!;
    private string _remoteDirectory = null!;
    private string _remoteFilePath = null!;

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

        _remoteFilePath = CombineRemotePath(_remoteDirectory, TestFileName);

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

        string localFilePath = Path.Combine(_tempDir, TestFileName);

        Assert.That(File.Exists(localFilePath), Is.True);
    }

    [Test]
    public async Task DownloadFileAsync_WhenRemoteFileExists_DownloadsCorrectContent()
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

        string localFilePath = Path.Combine(_tempDir, TestFileName);
        string content = File.ReadAllText(localFilePath);

        Assert.That(content, Does.Contain(ExpectedContent));
        Assert.That(content, Does.Contain("2026-06-08 14:00:00 test line"));
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
        Assert.That(progress.Messages, Has.Some.Contains(TestFileName));
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
        return $"{directory.TrimEnd('/')}/{fileName}";
    }

    private sealed class TestProgress : IProgress<string>
    {
        public List<string> Messages { get; } = new();

        public void Report(string value)
        {
            Messages.Add(value);
        }
    }
}