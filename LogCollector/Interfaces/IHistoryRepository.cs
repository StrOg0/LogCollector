namespace LogCollector.Interfaces;

public interface IHistoryRepository
{
    Task<List<CollectionHistory>> GetHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task SaveHistoryAsync(CollectionHistory record);
}