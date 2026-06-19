using LogCollectorApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LogCollectorApp.Services
{
    public interface IServerService
    {
        Task<List<ServerGroup>> GetAllGroupsAsync();
        Task<List<Server>> GetServersByGroupAsync(long groupId);
        Task<List<Server>> GetServersByIpMaskAsync(string ipMask);
    }
}