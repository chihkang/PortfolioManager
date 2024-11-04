using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using PortfolioManager.Models;

namespace PortfolioManager.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly ILogger<MongoDbService> _logger;
        private readonly MongoClient _client;

        public MongoDbService(IOptions<MongoDbSettings> settings, ILogger<MongoDbService> logger)
        {
            _logger = logger;
            try
            {
                var mongoSettings = MongoClientSettings.FromConnectionString(settings.Value.ConnectionString);
                mongoSettings.ServerApi = new ServerApi(ServerApiVersion.V1);
                
                _logger.LogInformation($"Attempting to connect to MongoDB with database: {settings.Value.DatabaseName}");
                _client = new MongoClient(mongoSettings);
                _database = _client.GetDatabase(settings.Value.DatabaseName);

                // Test connection and list collections
                TestConnection();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to MongoDB");
                throw;
            }
        }

        private void TestConnection()
        {
            try
            {
                // Test basic connection
                _database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                _logger.LogInformation("MongoDB connection test successful");

                // List all collections
                var collections = _database.ListCollections().ToList();
                _logger.LogInformation($"Found {collections.Count} collections in database {_database.DatabaseNamespace.DatabaseName}:");
                foreach (var collection in collections)
                {
                    _logger.LogInformation($"Collection: {collection["name"]}");
                }

                // Test access to each collection
                TestCollectionAccess<User>("Users");
                TestCollectionAccess<Portfolio>("Portfolios");
                TestCollectionAccess<Stock>("Stocks");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB connection test failed");
                throw new Exception("Failed to verify MongoDB connection and permissions", ex);
            }
        }

        private void TestCollectionAccess<T>(string collectionName)
        {
            try
            {
                var collection = _database.GetCollection<T>(collectionName);
                var count = collection.CountDocuments(new BsonDocument());
                _logger.LogInformation($"Successfully accessed {collectionName} collection. Document count: {count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to access {collectionName} collection");
                throw;
            }
        }

        // Users Collection
        private IMongoCollection<User> _users;
        public IMongoCollection<User> Users
        {
            get
            {
                if (_users == null)
                {
                    _users = _database.GetCollection<User>("users");
                    _logger.LogInformation("Initialized Users collection");
                }
                return _users;
            }
        }

        // Portfolios Collection
        private IMongoCollection<Portfolio> _portfolios;
        public IMongoCollection<Portfolio> Portfolios
        {
            get
            {
                if (_portfolios == null)
                {
                    _portfolios = _database.GetCollection<Portfolio>("portfolio");
                    _logger.LogInformation("Initialized Portfolios collection");
                }
                return _portfolios;
            }
        }

        // Stocks Collection
        private IMongoCollection<Stock> _stocks;
        public IMongoCollection<Stock> Stocks
        {
            get
            {
                if (_stocks == null)
                {
                    _stocks = _database.GetCollection<Stock>("stocks");
                    _logger.LogInformation("Initialized Stocks collection");
                }
                return _stocks;
            }
        }

        // Helper methods for diagnostics and testing
        public async Task<List<Stock>> TestStocksQuery()
        {
            try
            {
                var filter = Builders<Stock>.Filter.Empty;
                var stocks = await Stocks.Find(filter).ToListAsync();
                _logger.LogInformation($"TestStocksQuery found {stocks.Count} stocks");
                foreach (var stock in stocks)
                {
                    _logger.LogInformation($"Stock found: {stock.Name} ({stock.Alias}), Price: {stock.Price} {stock.Currency}");
                }
                return stocks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TestStocksQuery");
                throw;
            }
        }

        public async Task<DatabaseStatusInfo> GetDatabaseStatus()
        {
            try
            {
                var collections = await _database.ListCollectionNamesAsync();
                var status = new DatabaseStatusInfo
                {
                    DatabaseName = _database.DatabaseNamespace.DatabaseName,
                    Collections = collections.ToList(),
                    CollectionCounts = new Dictionary<string, long>()
                };

                // 獲取每個集合的文檔數量
                foreach (var collection in collections.ToList())
                {
                    var count = await _database.GetCollection<BsonDocument>(collection)
                        .CountDocumentsAsync(new BsonDocument());
                    status.CollectionCounts.Add(collection, count);
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database status");
                throw;
            }
        }
    }

    public class MongoDbSettings
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
    }

    public class DatabaseStatusInfo
    {
        public string DatabaseName { get; set; }
        public List<string> Collections { get; set; }
        public Dictionary<string, long> CollectionCounts { get; set; }
    }
}