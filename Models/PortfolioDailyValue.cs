using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Models;

public class PortfolioDailyValue
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string PortfolioId { get; set; }

    public DateTime Date { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalValueTWD { get; set; }
}