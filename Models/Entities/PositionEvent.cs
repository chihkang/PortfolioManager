using MongoDB.Bson.Serialization.Attributes;

namespace PortfolioManager.Models.Entities;

public class PositionEvent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// 操作唯一識別碼，用於防止重複操作（UUID）
    /// </summary>
    [BsonElement("operationId")]
    public required string OperationId { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public required string UserId { get; set; }

    [BsonElement("stockId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public required string StockId { get; set; }

    /// <summary>
    /// 交易類型：BUY 或 SELL
    /// </summary>
    [BsonElement("type")]
    public required string Type { get; set; }

    /// <summary>
    /// 交易時間
    /// </summary>
    [BsonElement("tradeAt")]
    public DateTime TradeAt { get; set; }

    /// <summary>
    /// 記錄建立時間
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 交易前數量
    /// </summary>
    [BsonElement("quantityBefore")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal QuantityBefore { get; set; }

    /// <summary>
    /// 交易後數量
    /// </summary>
    [BsonElement("quantityAfter")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal QuantityAfter { get; set; }

    /// <summary>
    /// 數量變化（正數為買入，負數為賣出）
    /// </summary>
    [BsonElement("quantityDelta")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal QuantityDelta { get; set; }

    /// <summary>
    /// 幣別
    /// </summary>
    [BsonElement("currency")]
    public required string Currency { get; set; }

    /// <summary>
    /// 交易後總成本
    /// </summary>
    [BsonElement("totalCostAfter")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TotalCostAfter { get; set; }

    /// <summary>
    /// 單位價格
    /// </summary>
    [BsonElement("unitPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// 來源平台：ios, android, web
    /// </summary>
    [BsonElement("source")]
    public required string Source { get; set; }

    /// <summary>
    /// 應用程式版本
    /// </summary>
    [BsonElement("appVersion")]
    public required string AppVersion { get; set; }
}
