using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LogCollectorApp.Data.Repositories
{
    public class ServerRepository : IServerRepository
    {
        private readonly AppDbContext _context;

        public ServerRepository(AppDbContext context)
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