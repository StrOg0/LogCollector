using LogCollectorApp.Data.Repositories;
using LogCollectorApp.Models;
using LogCollectorApp.Services;
using NUnit.Framework;

namespace LogCollectorApp.Tests.Services;

[TestFixture]
public class ServerServiceTests
{
    private FakeServerRepository _repository = null!;
    private ServerService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeServerRepository();
        _service = new ServerService(_repository);
    }

    [Test]
    public async Task GetAllGroupsAsync_WhenRepositoryContainsGroups_ReturnsGroups()
    {
        _repository.Groups.AddRange(new[]
        {
            new ServerGroup { Id = 1, Name = "web" },
            new ServerGroup { Id = 2, Name = "app" }
        });

        List<ServerGroup> result = await _service.GetAllGroupsAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Name, Is.EqualTo("web"));
        Assert.That(result[1].Name, Is.EqualTo("app"));
    }

    [Test]
    public async Task GetServersByGroupAsync_WhenGroupExists_ReturnsServersFromSelectedGroup()
    {
        _repository.Servers.AddRange(new[]
        {
            CreateValidServer(id: 1, groupId: 1, name: "web-1", ip: "10.10.130.1"),
            CreateValidServer(id: 2, groupId: 1, name: "web-2", ip: "10.10.130.2"),
            CreateValidServer(id: 3, groupId: 2, name: "app-1", ip: "10.10.140.1")
        });

        List<Server> result = await _service.GetServersByGroupAsync(1);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(x => x.Name), Is.EquivalentTo(new[] { "web-1", "web-2" }));
    }

    [Test]
    public async Task GetServersByIpMaskAsync_WhenMaskIsValid_ReturnsMatchingServers()
    {
        _repository.Servers.AddRange(new[]
        {
            CreateValidServer(id: 1, groupId: 1, name: "web-1", ip: "10.10.130.1"),
            CreateValidServer(id: 2, groupId: 1, name: "web-2", ip: "10.10.130.2"),
            CreateValidServer(id: 3, groupId: 2, name: "app-1", ip: "10.10.140.1")
        });

        List<Server> result = await _service.GetServersByIpMaskAsync("10.10.130.*");

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(_repository.LastIpMask, Is.EqualTo("10.10.130.*"));
        Assert.That(_repository.SearchByIpMaskCallCount, Is.EqualTo(1));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("10.10.130")]
    [TestCase("10.10.130.999")]
    [TestCase("10.10.130.abc")]
    public void GetServersByIpMaskAsync_WhenMaskIsInvalid_ThrowsArgumentException(string ipMask)
    {
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.GetServersByIpMaskAsync(ipMask);
        });

        Assert.That(_repository.SearchByIpMaskCallCount, Is.EqualTo(0));
    }

    [Test]
    public void UpdateServerAsync_WhenServerIsNull_ThrowsArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await _service.UpdateServerAsync(null!);
        });

        Assert.That(_repository.UpdateServerCallCount, Is.EqualTo(0));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void UpdateServerAsync_WhenServerNameIsEmpty_ThrowsArgumentException(string name)
    {
        Server server = CreateValidServer();
        server.Name = name;

        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.UpdateServerAsync(server);
        });

        Assert.That(_repository.UpdateServerCallCount, Is.EqualTo(0));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void UpdateServerAsync_WhenIpAddressIsEmpty_ThrowsArgumentException(string ipAddress)
    {
        Server server = CreateValidServer();
        server.IpAddress = ipAddress;

        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.UpdateServerAsync(server);
        });

        Assert.That(_repository.UpdateServerCallCount, Is.EqualTo(0));
    }

    [TestCase("10.10.130.999")]
    [TestCase("10.10.130.abc")]
    [TestCase("not-an-ip")]
    public void UpdateServerAsync_WhenIpAddressIsInvalid_ThrowsArgumentException(string ipAddress)
    {
        Server server = CreateValidServer();
        server.IpAddress = ipAddress;

        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.UpdateServerAsync(server);
        });

        Assert.That(_repository.UpdateServerCallCount, Is.EqualTo(0));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void UpdateServerAsync_WhenSshPortIsLessThanOne_ThrowsArgumentException(int port)
    {
        Server server = CreateValidServer();
        server.SshPort = port;

        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.UpdateServerAsync(server);
        });

        Assert.That(_repository.UpdateServerCallCount, Is.EqualTo(0));
    }

    [TestCase(65536)]
    [TestCase(100000)]
    public void UpdateServerAsync_WhenSshPortIsGreaterThan65535_ThrowsArgumentException(int port)
    {
        Server server = CreateValidServer();
        server.SshPort = port;

        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _service.UpdateServerAsync(server);
        });

        Assert.That(_repository.UpdateServerCallCount, Is.EqualTo(0));
    }

    [TestCase(1)]
    [TestCase(22)]
    [TestCase(65535)]
    public async Task UpdateServerAsync_WhenServerIsValid_CallsRepository(int port)
    {
        Server server = CreateValidServer();
        server.SshPort = port;

        await _service.UpdateServerAsync(server);

        Assert.That(_repository.UpdateServerCallCount, Is.EqualTo(1));
        Assert.That(_repository.UpdatedServer, Is.SameAs(server));
    }

    private static Server CreateValidServer(
        long id = 1,
        long groupId = 1,
        string name = "web-1",
        string ip = "10.10.130.6")
    {
        return new Server
        {
            Id = id,
            GroupId = groupId,
            Name = name,
            IpAddress = ip,
            SshPort = 22,
            IsActive = true
        };
    }

    private sealed class FakeServerRepository : IServerRepository
    {
        public List<ServerGroup> Groups { get; } = new();
        public List<Server> Servers { get; } = new();

        public int SearchByIpMaskCallCount { get; private set; }
        public int UpdateServerCallCount { get; private set; }

        public string? LastIpMask { get; private set; }
        public Server? UpdatedServer { get; private set; }

        public Task<List<ServerGroup>> GetAllGroupsAsync()
        {
            return Task.FromResult(Groups.ToList());
        }

        public Task<List<Server>> GetServersByGroupAsync(long groupId)
        {
            List<Server> result = Servers
                .Where(server => server.GroupId == groupId)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<List<Server>> SearchByIpMaskAsync(string ipMask)
        {
            SearchByIpMaskCallCount++;
            LastIpMask = ipMask;

            string patternStart = ipMask.Replace("*", "");

            List<Server> result = Servers
                .Where(server => server.IpAddress.StartsWith(patternStart))
                .ToList();

            return Task.FromResult(result);
        }

        public Task UpdateServerAsync(Server server)
        {
            UpdateServerCallCount++;
            UpdatedServer = server;

            Server? existingServer = Servers.FirstOrDefault(x => x.Id == server.Id);

            if (existingServer is null)
            {
                Servers.Add(server);
            }
            else
            {
                existingServer.Name = server.Name;
                existingServer.IpAddress = server.IpAddress;
                existingServer.SshPort = server.SshPort;
                existingServer.IsActive = server.IsActive;
            }

            return Task.CompletedTask;
        }
    }
}