using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Models.Entities;

public class PortfolioDailyValue
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? PortfolioId { get; set; }

    public DateTime Date { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    [BsonElement("TotalValueTWD")]
    public decimal TotalValueTwd { get; set; }
}