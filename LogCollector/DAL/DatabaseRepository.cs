using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogCollectorApp.DAl
{
    public class DatabaseRepository : IDatabaseRepository
    {

        public async Task<List<ServerGroup>> GetAllGroupsAsync()
        {
            using var context = new AppDbContext();

            return await context.ServerGroups
                .Where(g => g.IsActive)
                .ToListAsync();
        }

        public async Task<List<Server>> GetServersByGroupAsync(long groupId)
        {
            using var context = new AppDbContext();

            return await context.Servers
                .Include(s => s.Group)
                .Where(s => s.GroupId == groupId && s.IsActive)
                .ToListAsync();
        }

        public async Task<List<Server>> SearchByIpMaskAsync(string ipMask)
        {
            using var context = new AppDbContext();

            string sqlPattern = ipMask.Replace("*", "%");

            return await context.Servers
                .Include(s => s.Group)
                .Where(s => EF.Functions.Like(s.IpAddress, sqlPattern) && s.IsActive)
                .ToListAsync();
        }
    }
}