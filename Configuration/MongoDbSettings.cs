using System.Diagnostics.CodeAnalysis;
using PortfolioManager.Attribute;

namespace PortfolioManager.Configuration;

[Dto]
[SuppressMessage("Rider", "UnusedAutoPropertyAccessor.Global")]
public class MongoDbSettings
{
    public string? ConnectionString { get; set; }
    public string? DatabaseName { get; set; }
}