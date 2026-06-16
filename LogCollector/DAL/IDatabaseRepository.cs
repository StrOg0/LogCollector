using LogCollector.Models;

namespace LogCollector.App.DAL;

public interface IDatabaseRepository
{
    List<ServerGroup> GetServerGroups();
    List<Server> GetServersByGroup(int groupId);
}