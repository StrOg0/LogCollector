using LogCollectorApp.Data.Repositories;
using LogCollectorApp.Helpers;
using LogCollectorApp.Models;

namespace LogCollectorApp.Services;

public class ServerService : IServerService
{
    private readonly IServerRepository _repository;

    public ServerService(IServerRepository repository) => _repository = repository;

    public Task<List<ServerGroup>> GetAllGroupsAsync() => _repository.GetAllGroupsAsync();
    public Task<List<Server>> GetServersByGroupAsync(long groupId) => _repository.GetServersByGroupAsync(groupId);

    public Task<List<Server>> GetServersByIpMaskAsync(string ipMask)
    {
        if (!IpMaskHelper.IsValidIpMask(ipMask)) throw new ArgumentException($"Некорректный формат маски IP: '{ipMask}'");
        return _repository.SearchByIpMaskAsync(ipMask);
    }

    public Task UpdateServerAsync(Server server)
    {
        if (server == null) throw new ArgumentNullException(nameof(server));
        if (string.IsNullOrWhiteSpace(server.Name)) throw new ArgumentException("Название сервера не может быть пустым");
        if (string.IsNullOrWhiteSpace(server.IpAddress)) throw new ArgumentException("IP-адрес не может быть пустым");
        if (!IpAddressDbConverter.IsValid(server.IpAddress)) throw new ArgumentException($"Некорректный IP-адрес: {server.IpAddress}");
        if (server.SshPort is < 1 or > 65535) throw new ArgumentException("Порт SSH должен быть в диапазоне от 1 до 65535");

        return _repository.UpdateServerAsync(server);
    }
}
