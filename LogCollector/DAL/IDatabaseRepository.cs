using LogCollector.Models;

namespace LogCollector.DAL;

public interface IDatabaseRepository
{
    List<ServerGroup> GetServerGroups();
    List<Server> GetServersByGroup(int groupId);
}