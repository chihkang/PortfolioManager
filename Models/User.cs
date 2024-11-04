namespace PortfolioManager.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    [BsonElement("username")]
    public string Username { get; set; }
    [BsonElement("email")]
    public string Email { get; set; }

    [BsonElement("portfolioId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PortfolioId { get; set; }
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
    [BsonElement("settings")]
    public Dictionary<string, object> Settings { get; set; }
}
public class Portfolio
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    [BsonElement("totalValue")]
    public decimal TotalValue { get; set; }
    [BsonElement("exchange_rate")]
    public string ExchangeRate { get; set; }
    [BsonElement("exchange_rate_updated")]
    public DateTime ExchangeRateUpdated { get; set; }
    [BsonElement("lastUpdated")]
    public DateTime LastUpdated { get; set; }
    [BsonElement("stocks")]
    public List<PortfolioStock> Stocks { get; set; } = new List<PortfolioStock>();
}

public class PortfolioStock
{
    [BsonElement("stockId")]

    [BsonRepresentation(BsonType.ObjectId)]
    public string StockId { get; set; }

    [BsonElement("quantity")]
    public decimal Quantity { get; set; }
    [BsonElement("percentageOfTotal")]
    public decimal PercentageOfTotal { get; set; }
}


public class Stock
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    [BsonElement("name")]
    public string Name { get; set; }
    [BsonElement("alias")]
    public string Alias { get; set; }
    [BsonElement("price")]
    public decimal Price { get; set; }
    [BsonElement("currency")]
    public string Currency { get; set; }
    [BsonElement("lastUpdated")]
    public DateTime LastUpdated { get; set; }
}