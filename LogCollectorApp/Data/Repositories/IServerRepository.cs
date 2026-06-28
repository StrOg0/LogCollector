using LogCollectorApp.Models;

namespace LogCollectorApp.Data.Repositories;

public interface IServerRepository
{
    Task<List<ServerGroup>> GetAllGroupsAsync();
    Task<List<Server>> GetServersByGroupAsync(long groupId);
    Task<List<Server>> SearchByIpMaskAsync(string ipMask);
    Task UpdateServerAsync(Server server);
}
