using LogCollectorApp.Models;

namespace LogCollectorApp.DAl
{
    public interface IDatabaseRepository
    {
        Task<List<ServerGroup>> GetAllGroupsAsync();
        Task<List<Server>> GetServersByGroupAsync(long groupId);
        Task<List<Server>> SearchByIpMaskAsync(string ipMask);
    }
}