using LogCollectorApp.Data;
using LogCollectorApp.Data.Repositories;
using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LogCollectorApp.Tests.Data;

[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class ServerRepositoryIntegrationTests
{
    private DbContextOptions<AppDbContext> _options = null!;
    private string _connectionString = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connectionString = GetRequiredEnvironmentVariable("LOGCOLLECTOR_TEST_DB_CONNECTION");

        EnsureTestDatabase(_connectionString);

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        await ResetDatabaseAsync();
    }

    [Test]
    public async Task AppDbContext_WhenDatabaseCreated_CreatesRequiredTables()
    {
        await using var context = CreateContext();

        bool canConnect = await context.Database.CanConnectAsync();

        Assert.That(canConnect, Is.True);
        Assert.That(context.ServerGroups, Is.Not.Null);
        Assert.That(context.Servers, Is.Not.Null);
        Assert.That(context.LogSources, Is.Not.Null);
    }

    [Test]
    public async Task GetAllGroupsAsync_ReturnsOnlyActiveGroupsOrderedById()
    {
        await using var context = CreateContext();

        context.ServerGroups.AddRange(
            new ServerGroup { Name = "web", IsActive = true },
            new ServerGroup { Name = "app", IsActive = true },
            new ServerGroup { Name = "old", IsActive = false });

        await context.SaveChangesAsync();

        var repository = new ServerRepository(context);

        List<ServerGroup> result = await repository.GetAllGroupsAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(x => x.Name), Is.EqualTo(new[] { "web", "app" }));
        Assert.That(result.All(x => x.IsActive), Is.True);
    }

    [Test]
    public async Task GetServersByGroupAsync_ReturnsOnlyActiveServersFromSelectedGroup()
    {
        await using var context = CreateContext();

        ServerGroup webGroup = new() { Name = "web", IsActive = true };
        ServerGroup appGroup = new() { Name = "app", IsActive = true };

        context.ServerGroups.AddRange(webGroup, appGroup);
        await context.SaveChangesAsync();

        context.Servers.AddRange(
            CreateServer(webGroup.Id, "web-1", "10.10.130.1", true),
            CreateServer(webGroup.Id, "web-2", "10.10.130.2", true),
            CreateServer(webGroup.Id, "web-disabled", "10.10.130.3", false),
            CreateServer(appGroup.Id, "app-1", "10.10.140.1", true));

        await context.SaveChangesAsync();

        var repository = new ServerRepository(context);

        List<Server> result = await repository.GetServersByGroupAsync(webGroup.Id);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(x => x.Name), Is.EqualTo(new[] { "web-1", "web-2" }));
        Assert.That(result.All(x => x.GroupId == webGroup.Id), Is.True);
        Assert.That(result.All(x => x.IsActive), Is.True);
        Assert.That(result.All(x => x.Group is not null), Is.True);
    }

    [Test]
    public async Task SearchByIpMaskAsync_WhenMaskMatchesServers_ReturnsOnlyMatchingActiveServers()
    {
        await using var context = CreateContext();

        ServerGroup webGroup = new() { Name = "web", IsActive = true };
        context.ServerGroups.Add(webGroup);
        await context.SaveChangesAsync();

        context.Servers.AddRange(
            CreateServer(webGroup.Id, "web-1", "10.10.130.1", true),
            CreateServer(webGroup.Id, "web-2", "10.10.130.2", true),
            CreateServer(webGroup.Id, "web-disabled", "10.10.130.3", false),
            CreateServer(webGroup.Id, "other", "10.10.140.1", true));

        await context.SaveChangesAsync();

        var repository = new ServerRepository(context);

        List<Server> result = await repository.SearchByIpMaskAsync("10.10.130.*");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(x => x.Name), Is.EqualTo(new[] { "web-1", "web-2" }));
        Assert.That(result.All(x => x.IpAddress.StartsWith("10.10.130.")), Is.True);
        Assert.That(result.All(x => x.IsActive), Is.True);
    }

    [Test]
    public async Task UpdateServerAsync_WhenServerExists_UpdatesServerFieldsAndTrimsStrings()
    {
        await using var context = CreateContext();

        ServerGroup group = new() { Name = "web", IsActive = true };
        context.ServerGroups.Add(group);
        await context.SaveChangesAsync();

        Server server = CreateServer(group.Id, "old-name", "10.10.130.1", true);
        context.Servers.Add(server);
        await context.SaveChangesAsync();

        var repository = new ServerRepository(context);

        server.Name = "  new-name  ";
        server.IpAddress = " 10.10.130.99 ";
        server.SshPort = 2222;
        server.IsActive = false;

        await repository.UpdateServerAsync(server);

        await using var verifyContext = CreateContext();

        Server updated = await verifyContext.Servers.SingleAsync(x => x.Id == server.Id);

        Assert.That(updated.Name, Is.EqualTo("new-name"));
        Assert.That(updated.IpAddress, Is.EqualTo("10.10.130.99"));
        Assert.That(updated.SshPort, Is.EqualTo(2222));
        Assert.That(updated.IsActive, Is.False);
    }

    [Test]
    public async Task UpdateServerAsync_WhenServerDoesNotExist_ThrowsKeyNotFoundException()
    {
        await using var context = CreateContext();

        var repository = new ServerRepository(context);

        Server missingServer = new()
        {
            Id = 999,
            GroupId = 1,
            Name = "missing",
            IpAddress = "10.10.130.1",
            SshPort = 22,
            IsActive = true
        };

        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            await repository.UpdateServerAsync(missingServer);
        });
    }

    [Test]
    public async Task IpAddressMapping_WhenServerSavedAndLoaded_PreservesIpAddressAsString()
    {
        await using var context = CreateContext();

        ServerGroup group = new() { Name = "web", IsActive = true };
        context.ServerGroups.Add(group);
        await context.SaveChangesAsync();

        Server server = CreateServer(group.Id, "web-1", "192.168.1.10", true);
        context.Servers.Add(server);
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        Server loaded = await context.Servers.SingleAsync(x => x.Id == server.Id);

        Assert.That(loaded.IpAddress, Is.EqualTo("192.168.1.10"));
    }

    private async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();

        await context.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS pm02 CASCADE;");
        await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA pm02;");
        await context.Database.EnsureCreatedAsync();
    }

    private AppDbContext CreateContext()
    {
        return new AppDbContext(_options);
    }

    private static Server CreateServer(
        long groupId,
        string name,
        string ipAddress,
        bool isActive)
    {
        return new Server
        {
            GroupId = groupId,
            Name = name,
            IpAddress = ipAddress,
            SshPort = 22,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string GetRequiredEnvironmentVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);

        if (string.IsNullOrWhiteSpace(value))
            Assert.Ignore($"Интеграционный DB-тест пропущен: переменная окружения {name} не задана.");

        return value;
    }

    private static void EnsureTestDatabase(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!builder.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail(
                "Интеграционные DB-тесты можно запускать только на тестовой базе. " +
                "Имя базы данных должно содержать 'test'.");
        }
    }
}