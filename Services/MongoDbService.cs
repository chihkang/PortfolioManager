using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using PortfolioManager.Configuration;
using PortfolioManager.Models;

namespace PortfolioManager.Services;

public class MongoDbService
{
    // 使用常量定義重試策略
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 1000;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbService> _logger;
    private readonly Lazy<IMongoCollection<PortfolioDailyValue>> _portfolioDailyValues;
    private readonly Lazy<IMongoCollection<Portfolio>> _portfolios;
    private readonly Lazy<IMongoCollection<Stock>> _stocks;
    private readonly Lazy<IMongoCollection<User>> _users;

    public MongoDbService(IOptions<MongoDbSettings> settings, ILogger<MongoDbService> logger)
    {
        ArgumentNullException.ThrowIfNull(settings.Value.ConnectionString);
        ArgumentNullException.ThrowIfNull(settings.Value.DatabaseName);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        try
        {
            // Initialize in constructor
            var mongoSettings = MongoClientSettings.FromConnectionString(settings.Value.ConnectionString);
            mongoSettings.ServerApi = new ServerApi(ServerApiVersion.V1);

            _client = new MongoClient(mongoSettings);
            _database = _client.GetDatabase(settings.Value.DatabaseName);

            // Initialize lazy collections
            _users = new Lazy<IMongoCollection<User>>(() => _database.GetCollection<User>("users"));
            _portfolios = new Lazy<IMongoCollection<Portfolio>>(() =>
            {
                var collection = _database.GetCollection<Portfolio>("portfolio"); // 這裡改為 portfolio
                _logger.LogInformation("Initialized Portfolio collection");
                return collection;
            });
            _portfolioDailyValues = new Lazy<IMongoCollection<PortfolioDailyValue>>(() =>
                _database.GetCollection<PortfolioDailyValue>("portfolio_daily_values"));
            _stocks = new Lazy<IMongoCollection<Stock>>(() => _database.GetCollection<Stock>("stocks"));

            TestConnection();
            CreateIndexesAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MongoDB service");
            throw;
        }
    }

    // 公開屬性使用只讀訪問器
    public IMongoCollection<User> Users => _users.Value;
    public IMongoCollection<Portfolio> Portfolios => _portfolios.Value;
    public IMongoCollection<PortfolioDailyValue> PortfolioDailyValues => _portfolioDailyValues.Value;
    public IMongoCollection<Stock> Stocks => _stocks.Value;
    public IMongoClient Client => _client;

    private async Task CreateIndexesAsync()
    {
        try
        {
            _logger.LogInformation("Starting index creation...");

            await Task.WhenAll(
                CreateStockIndexes(),
                CreatePortfolioIndexes(),
                CreateUserIndexes(),
                CreatePortfolioDailyValueIndexes()
            );

            _logger.LogInformation("Successfully created all indexes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create indexes");
            throw;
        }
    }

    private async Task CreateStockIndexes()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Stock>(
                Builders<Stock>.IndexKeys.Ascending(s => s.Name),
                new CreateIndexOptions { Unique = true, Name = "idx_stock_name" }
            ),
            new CreateIndexModel<Stock>(
                Builders<Stock>.IndexKeys.Ascending(s => s.Alias),
                new CreateIndexOptions { Sparse = true, Name = "idx_stock_alias" }
            ),
            new CreateIndexModel<Stock>(
                Builders<Stock>.IndexKeys
                    .Ascending(s => s.Currency)
                    .Ascending(s => s.Price),
                new CreateIndexOptions { Name = "idx_stock_currency_price" }
            )
        };

        await CreateIndexesWithRetry(Stocks, indexes, nameof(Stocks));
    }

    private async Task CreatePortfolioIndexes()
    {
        var indexes = new[]
        {
            new CreateIndexModel<Portfolio>(
                Builders<Portfolio>.IndexKeys
                    .Ascending(p => p.UserId),
                new CreateIndexOptions
                {
                    Name = "idx_portfolio_user_id"
                }
            ),
            new CreateIndexModel<Portfolio>(
                Builders<Portfolio>.IndexKeys
                    .Ascending(p => p.UserId)
                    .Descending(p => p.LastUpdated),
                new CreateIndexOptions
                {
                    Name = "idx_portfolio_user_updated"
                }
            )
        };

        await CreateIndexesWithRetry(Portfolios, indexes, "portfolio");
    }

    private async Task CreateUserIndexes()
    {
        var indexes = new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "idx_user_email" }
            ),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Name = "idx_user_username" }
            )
        };

        await CreateIndexesWithRetry(Users, indexes, nameof(Users));
    }

    private async Task CreatePortfolioDailyValueIndexes()
    {
        var indexes = new[]
        {
            new CreateIndexModel<PortfolioDailyValue>(
                Builders<PortfolioDailyValue>.IndexKeys
                    .Ascending(p => p.PortfolioId)
                    .Ascending(p => p.Date),
                new CreateIndexOptions
                {
                    Name = "idx_portfolio_daily_value_portfolio_date"
                }
            ),
            new CreateIndexModel<PortfolioDailyValue>(
                Builders<PortfolioDailyValue>.IndexKeys
                    .Ascending(p => p.Date),
                new CreateIndexOptions
                {
                    Name = "idx_portfolio_daily_value_date"
                }
            )
        };

        await CreateIndexesWithRetry(PortfolioDailyValues, indexes, nameof(PortfolioDailyValues));
    }

    private async Task CreateIndexesWithRetry<T>(
        IMongoCollection<T> collection,
        CreateIndexModel<T>[] indexes,
        string collectionName)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
            try
            {
                var existingIndexes = await (await collection.Indexes
                        .ListAsync())
                    .ToListAsync();

                var existingIndexNames = existingIndexes
                    .Select(idx => idx["name"].AsString)
                    .ToHashSet();

                foreach (var index in indexes)
                    if (!existingIndexNames.Contains(index.Options.Name))
                    {
                        await collection.Indexes.CreateOneAsync(index);
                        _logger.LogInformation(
                            "Created index '{IndexName}' for {CollectionName} collection",
                            index.Options.Name,
                            collectionName);
                    }

                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "Failed to create indexes for {CollectionName} (Attempt {Attempt}/{MaxRetries})",
                    collectionName,
                    attempt,
                    MaxRetries);

                await Task.Delay(BaseDelayMs * attempt);
            }
    }

    private void TestConnection()
    {
        try
        {
            // 測試基本連接
            _logger.LogInformation("Attempting to connect to MongoDB...");
            _database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            _logger.LogInformation("Successfully connected to MongoDB");

            // 獲取數據庫信息
            var databaseStats = _database.RunCommand<BsonDocument>(new BsonDocument("dbStats", 1));
            _logger.LogInformation("Database stats: {@DatabaseStats}", databaseStats);

            // 列出並測試所有集合
            var collections = _database.ListCollections().ToList();
            _logger.LogInformation("Found {CollectionCount} collections", collections.Count);

            foreach (var collection in collections)
            {
                var collectionName = collection["name"].AsString;
                var collectionStats = _database.RunCommand<BsonDocument>(
                    new BsonDocument("collStats", collectionName));
                _logger.LogInformation(
                    "Collection {CollectionName} stats: {@CollectionStats}",
                    collectionName,
                    collectionStats);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB connection verification failed");
            throw new InvalidOperationException(
                "Failed to verify MongoDB connection. Details: " + ex.Message,
                ex);
        }
    }

    public async Task<Stock[]> TestStocksQuery()
    {
        try
        {
            var filter = Builders<Stock>.Filter.Empty;
            var stocks = await Stocks.Find(filter).ToListAsync();

            _logger.LogInformation("TestStocksQuery retrieved {StockCount} stocks", stocks.Count);

            foreach (var stock in stocks)
                _logger.LogInformation(
                    "Stock: {StockName} ({StockAlias}), Price: {StockPrice} {StockCurrency}",
                    stock.Name,
                    stock.Alias ?? "N/A",
                    stock.Price,
                    stock.Currency
                );

            return stocks.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during TestStocksQuery execution");
            throw;
        }
    }

    public async Task<DatabaseStatusInfo> GetDatabaseStatus()
    {
        try
        {
            var collections = await (await _database.ListCollectionNamesAsync()).ToListAsync();
            var status = new DatabaseStatusInfo
            {
                DatabaseName = _database.DatabaseNamespace.DatabaseName,
                Collections = collections,
                CollectionCounts = new Dictionary<string, long>()
            };

            foreach (var collection in collections)
            {
                var count = await _database.GetCollection<BsonDocument>(collection)
                    .CountDocumentsAsync(new BsonDocument());
                status.CollectionCounts[collection] = count;
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