using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Models.Entities;

public class Portfolio
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? UserId { get; set; }

    [BsonElement("lastUpdated")] public DateTime LastUpdated { get; set; }

    [BsonElement("stocks")] public List<PortfolioStock> Stocks { get; set; } = new();
}