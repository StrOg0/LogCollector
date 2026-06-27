using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
public class SshFileHandlerTests
{
    private SshFileHandler _handler = null!;
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new SshFileHandler();
        _tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void TearDown()
    {
        _handler?.Dispose();

        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetFilesListAsync_WhenHostIsEmpty_ThrowsArgumentException(string? host)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.GetFilesListAsync(
                host!,
                22,
                "user",
                "password",
                "/var/log",
                CancellationToken.None);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetFilesListAsync_WhenUsernameIsEmpty_ThrowsArgumentException(string? username)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.GetFilesListAsync(
                "127.0.0.1",
                22,
                username!,
                "password",
                "/var/log",
                CancellationToken.None);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetFilesListAsync_WhenPasswordIsEmpty_ThrowsArgumentException(string? password)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.GetFilesListAsync(
                "127.0.0.1",
                22,
                "user",
                password!,
                "/var/log",
                CancellationToken.None);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(65536)]
    [TestCase(100000)]
    public void GetFilesListAsync_WhenPortIsInvalid_ThrowsArgumentException(int port)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.GetFilesListAsync(
                "127.0.0.1",
                port,
                "user",
                "password",
                "/var/log",
                CancellationToken.None);
        });
    }

    [Test]
    public void GetFilesListAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _handler.GetFilesListAsync(
                "127.0.0.1",
                22,
                "user",
                "password",
                "/var/log",
                cts.Token);
        });
    }

    [Test]
    public void GetFilesListAsync_WhenHandlerDisposed_ThrowsObjectDisposedException()
    {
        _handler.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _handler.GetFilesListAsync(
                "127.0.0.1",
                22,
                "user",
                "password",
                "/var/log",
                CancellationToken.None);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void DownloadFileAsync_WhenHostIsEmpty_ThrowsArgumentException(string? host)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.DownloadFileAsync(
                host!,
                22,
                "user",
                "password",
                "/var/log/app.log",
                _tempRoot,
                new Progress<string>(),
                CancellationToken.None);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void DownloadFileAsync_WhenUsernameIsEmpty_ThrowsArgumentException(string? username)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.DownloadFileAsync(
                "127.0.0.1",
                22,
                username!,
                "password",
                "/var/log/app.log",
                _tempRoot,
                new Progress<string>(),
                CancellationToken.None);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void DownloadFileAsync_WhenPasswordIsEmpty_ThrowsArgumentException(string? password)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.DownloadFileAsync(
                "127.0.0.1",
                22,
                "user",
                password!,
                "/var/log/app.log",
                _tempRoot,
                new Progress<string>(),
                CancellationToken.None);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(65536)]
    [TestCase(100000)]
    public void DownloadFileAsync_WhenPortIsInvalid_ThrowsArgumentException(int port)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _handler.DownloadFileAsync(
                "127.0.0.1",
                port,
                "user",
                "password",
                "/var/log/app.log",
                _tempRoot,
                new Progress<string>(),
                CancellationToken.None);
        });
    }

    [Test]
    public void DownloadFileAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _handler.DownloadFileAsync(
                "127.0.0.1",
                22,
                "user",
                "password",
                "/var/log/app.log",
                _tempRoot,
                new Progress<string>(),
                cts.Token);
        });

        Assert.That(Directory.Exists(_tempRoot), Is.False);
    }

    [Test]
    public void DownloadFileAsync_WhenHandlerDisposed_ThrowsObjectDisposedException()
    {
        _handler.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _handler.DownloadFileAsync(
                "127.0.0.1",
                22,
                "user",
                "password",
                "/var/log/app.log",
                _tempRoot,
                new Progress<string>(),
                CancellationToken.None);
        });
    }
}