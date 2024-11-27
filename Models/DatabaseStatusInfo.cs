namespace PortfolioManager.Models;

public class DatabaseStatusInfo
{
    public DatabaseStatusInfo()
    {
        Collections = new List<string>();
        CollectionCounts = new Dictionary<string, long>();
    }

    public DatabaseStatusInfo(string databaseName)
    {
        DatabaseName = databaseName;
        Collections = new List<string>();
        CollectionCounts = new Dictionary<string, long>();
    }

    public required string DatabaseName { get; set; }
    public required List<string> Collections { get; set; }
    public required Dictionary<string, long> CollectionCounts { get; set; }
}