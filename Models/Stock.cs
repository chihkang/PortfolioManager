using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Models;

public class Stock
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")] public string? Name { get; set; }

    [BsonElement("alias")] public string? Alias { get; set; }

    [BsonElement("price")] public decimal Price { get; set; }

    [BsonElement("currency")] public string? Currency { get; set; }

    [BsonElement("lastUpdated")] public DateTime LastUpdated { get; set; }
}