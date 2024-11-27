using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("username")] public string? Username { get; set; }

    [BsonElement("email")] public string? Email { get; set; }

    [BsonElement("portfolioId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? PortfolioId { get; set; }

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }

    [BsonElement("settings")] public Dictionary<string, object>? Settings { get; set; }
}