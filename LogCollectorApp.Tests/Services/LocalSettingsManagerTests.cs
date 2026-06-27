using LogCollectorApp.Models;
using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
[NonParallelizable]
public class LocalSettingsManagerTests
{
    private string _settingsPath = null!;
    private string? _originalContent;
    private bool _originalFileExists;

    [SetUp]
    public void SetUp()
    {
        _settingsPath = LocalSettingsManager.SettingsPath;

        string? settingsDirectory = Path.GetDirectoryName(_settingsPath);
        if (settingsDirectory is not null)
            Directory.CreateDirectory(settingsDirectory);

        _originalFileExists = File.Exists(_settingsPath);
        _originalContent = _originalFileExists
            ? File.ReadAllText(_settingsPath)
            : null;

        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);

        if (_originalFileExists && _originalContent is not null)
        {
            string? settingsDirectory = Path.GetDirectoryName(_settingsPath);
            if (settingsDirectory is not null)
                Directory.CreateDirectory(settingsDirectory);

            File.WriteAllText(_settingsPath, _originalContent);
        }
    }

    [Test]
    public void SettingsPath_ReturnsPathToSettingsJsonInAppData()
    {
        string path = LocalSettingsManager.SettingsPath;

        Assert.That(path, Does.EndWith(Path.Combine("LogCollectorApp", "settings.json")));
    }

    [Test]
    public void Load_WhenSettingsFileDoesNotExist_ReturnsDefaultSettings()
    {
        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);

        AppSettings result = LocalSettingsManager.Load();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.OutputPath, Is.Null);
        Assert.That(result.SshUsername, Is.Null);
        Assert.That(result.SshPassword, Is.Null);
    }

    [Test]
    public void Save_WhenSettingsProvided_CreatesSettingsFile()
    {
        var settings = new AppSettings
        {
            OutputPath = @"C:\Logs",
            SshUsername = "test_user",
            SshPassword = "test_password"
        };

        LocalSettingsManager.Save(settings);

        Assert.That(File.Exists(_settingsPath), Is.True);
    }

    [Test]
    public void Save_WhenSettingsProvided_WritesSettingsToFile()
    {
        var settings = new AppSettings
        {
            OutputPath = @"C:\Logs",
            SshUsername = "test_user",
            SshPassword = "test_password"
        };

        LocalSettingsManager.Save(settings);

        string json = File.ReadAllText(_settingsPath);

        Assert.That(json, Does.Contain("OutputPath"));
        Assert.That(json, Does.Contain(@"C:\\Logs"));
        Assert.That(json, Does.Contain("SshUsername"));
        Assert.That(json, Does.Contain("test_user"));
        Assert.That(json, Does.Contain("SshPassword"));
        Assert.That(json, Does.Contain("test_password"));
    }

    [Test]
    public void Load_WhenSettingsFileContainsValidJson_ReturnsSettings()
    {
        var settings = new AppSettings
        {
            OutputPath = @"D:\ResultLogs",
            SshUsername = "admin",
            SshPassword = "12345"
        };

        LocalSettingsManager.Save(settings);

        AppSettings result = LocalSettingsManager.Load();

        Assert.That(result.OutputPath, Is.EqualTo(@"D:\ResultLogs"));
        Assert.That(result.SshUsername, Is.EqualTo("admin"));
        Assert.That(result.SshPassword, Is.EqualTo("12345"));
    }

    [Test]
    public void Load_WhenSettingsFileContainsInvalidJson_ReturnsDefaultSettings()
    {
        File.WriteAllText(_settingsPath, "this is not valid json");

        AppSettings result = LocalSettingsManager.Load();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.OutputPath, Is.Null);
        Assert.That(result.SshUsername, Is.Null);
        Assert.That(result.SshPassword, Is.Null);
    }

    [Test]
    public void Load_WhenSettingsFileContainsJsonNull_ReturnsDefaultSettings()
    {
        File.WriteAllText(_settingsPath, "null");

        AppSettings result = LocalSettingsManager.Load();

        Assert.That(result, Is.Not.Null);
        Assert.That(result.OutputPath, Is.Null);
        Assert.That(result.SshUsername, Is.Null);
        Assert.That(result.SshPassword, Is.Null);
    }

    [Test]
    public void Save_WhenSettingsContainsNullValues_CanBeLoadedBack()
    {
        var settings = new AppSettings
        {
            OutputPath = null,
            SshUsername = null,
            SshPassword = null
        };

        LocalSettingsManager.Save(settings);

        AppSettings result = LocalSettingsManager.Load();

        Assert.That(result.OutputPath, Is.Null);
        Assert.That(result.SshUsername, Is.Null);
        Assert.That(result.SshPassword, Is.Null);
    }
}