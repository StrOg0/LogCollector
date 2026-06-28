using LogCollectorApp.Models;

namespace LogCollectorApp.Services;

public interface IServerService
{
    Task<List<ServerGroup>> GetAllGroupsAsync();
    Task<List<Server>> GetServersByGroupAsync(long groupId);
    Task<List<Server>> GetServersByIpMaskAsync(string ipMask);
    Task UpdateServerAsync(Server server);
}
