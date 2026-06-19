using LogCollectorApp.Data.Repositories;
using LogCollectorApp.Helpers;
using LogCollectorApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LogCollectorApp.Services
{
    public class ServerService : IServerService
    {
        private readonly IServerRepository _serverRepository;

        public ServerService(IServerRepository serverRepository)
        {
            _serverRepository = serverRepository;
        }

        public async Task<List<ServerGroup>> GetAllGroupsAsync()
        {
            return await _serverRepository.GetAllGroupsAsync();
        }

        public async Task<List<Server>> GetServersByGroupAsync(long groupId)
        {
            return await _serverRepository.GetServersByGroupAsync(groupId);
        }

        public async Task<List<Server>> GetServersByIpMaskAsync(string ipMask)
        {
            if (!IpMaskHelper.IsValidIpMask(ipMask))
            {
                throw new ArgumentException($"Некорректный формат маски IP: '{ipMask}'");
            }

            return await _serverRepository.SearchByIpMaskAsync(ipMask);
        }
    }
}