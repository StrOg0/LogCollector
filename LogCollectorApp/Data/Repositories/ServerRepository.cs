using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogCollectorApp.Data.Repositories;

public class ServerRepository : IServerRepository
{
    private readonly AppDbContext _context;

    public ServerRepository(AppDbContext context) => _context = context;

    public Task<List<ServerGroup>> GetAllGroupsAsync()
    {
        _context.ChangeTracker.Clear();
        return _context.ServerGroups.AsNoTracking()
            .Include(g => g.LogSource)
            .Where(g => g.IsActive)
            .OrderBy(g => g.Id)
            .ToListAsync();
    }

    public Task<List<Server>> GetServersByGroupAsync(long groupId)
    {
        _context.ChangeTracker.Clear();
        return _context.Servers.AsNoTracking()
            .Include(s => s.Group)
                .ThenInclude(g => g!.LogSource)
            .Where(s => s.GroupId == groupId && s.IsActive)
            .OrderBy(s => s.Id)
            .ToListAsync();
    }

    public async Task UpdateServerAsync(Server server)
    {
        _context.ChangeTracker.Clear();
        var entity = await _context.Servers.FirstOrDefaultAsync(s => s.Id == server.Id)
            ?? throw new KeyNotFoundException($"Сервер с ID {server.Id} не найден");

        entity.GroupId = server.GroupId;
        entity.Name = server.Name.Trim();
        entity.IpAddress = server.IpAddress.Trim();
        entity.SshPort = server.SshPort;
        entity.IsActive = server.IsActive;

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    public Task<List<Server>> SearchByIpMaskAsync(string ipMask)
    {
        _context.ChangeTracker.Clear();
        string pattern = ipMask.Replace("*", "%");

        return _context.Servers
            .FromSqlInterpolated($"""
                SELECT id, group_id, name, ip_address, ssh_port, is_active, created_at
                FROM pm02.servers
                WHERE is_active = true
                  AND ip_address::text LIKE {pattern}
                """)
            .AsNoTracking()
            .Include(s => s.Group)
                .ThenInclude(g => g!.LogSource)
            .OrderBy(s => s.Id)
            .ToListAsync();
    }
}
