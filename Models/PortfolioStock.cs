using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Models;

public class PortfolioStock
{
    [BsonElement("stockId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? StockId { get; set; }

    [BsonElement("quantity")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; }
}