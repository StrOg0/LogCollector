using LogCollector.DAL;

namespace LogCollector.BLL;

public class LogCollectionService
{
    private readonly IDatabaseRepository _repository;

    public LogCollectionService(IDatabaseRepository repository)
    {
        _repository = repository;
    }
}