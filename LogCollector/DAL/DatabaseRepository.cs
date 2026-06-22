using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogCollectorApp.DAl
{
    public class DatabaseRepository : IDatabaseRepository
    {
        private readonly AppDbContext _context;

        public DatabaseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ServerGroup>> GetAllGroupsAsync()
        {
            return await _context.ServerGroups
                .Where(g => g.IsActive)
                .ToListAsync();
        }

        public async Task<List<Server>> GetServersByGroupAsync(long groupId)
        {
            return await _context.Servers
                .Include(s => s.Group)
                .Where(s => s.GroupId == groupId && s.IsActive)
                .ToListAsync();
        }

        public async Task<List<Server>> SearchByIpMaskAsync(string ipMask)
        {
            string sqlPattern = ipMask.Replace("*", "%");

            return await _context.Servers
                .Include(s => s.Group)
                .Where(s => EF.Functions.Like(s.IpAddress, sqlPattern) && s.IsActive)
                .ToListAsync();
        }
    }
}