# Tutorial: Creating Your First User and Portfolio

This tutorial will guide you through creating a user, managing their portfolio, and tracking portfolio value using the PortfolioManager API.

## Prerequisites

- PortfolioManager running locally or deployed
- Access to Swagger UI or an HTTP client (curl, Postman, etc.)
- MongoDB connection configured

## Step 1: Access Swagger UI

Open your browser and navigate to:

- **Local**: `http://localhost:3000/swagger`
- **Production**: `https://your-domain.com/swagger`

Swagger UI provides an interactive interface for testing API endpoints.

## Step 2: Create a User

Creating a user automatically creates an associated empty portfolio.

### Using Swagger UI

1. Find the **POST /api/User** endpoint
2. Click **"Try it out"**
3. Enter the request body:

```json
{
  "username": "alice",
  "email": "alice@example.com"
}
```

4. Click **"Execute"**

### Using curl

```bash
curl -X POST http://localhost:3000/api/User \
  -H "Content-Type: application/json" \
  -d '{
    "username": "alice",
    "email": "alice@example.com"
  }'
```

### Expected Response

```json
{
  "id": "67890abcdef12345",
  "username": "alice",
  "email": "alice@example.com",
  "portfolioId": "12345abcdef67890",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

**Note**: Save the `portfolioId` - you'll need it in the next steps!

## Step 3: Add Stocks to the Database

Before adding stocks to a portfolio, they must exist in the database. In a real system, stocks would be pre-populated or added via an admin interface.

For this tutorial, you can manually insert stocks into MongoDB or use an API endpoint if available.

**Example stock document:**

```json
{
  "name": "Apple Inc.",
  "alias": "AAPL",
  "price": 150.25,
  "currency": "USD",
  "lastUpdated": "2025-01-15T10:00:00Z"
}
```

## Step 4: Get Portfolio by Username

Retrieve the enriched portfolio (with full stock details) by username.

### Using Swagger UI

1. Find **GET /api/Portfolio/user/{username}**
2. Click **"Try it out"**
3. Enter username: `alice`
4. Click **"Execute"**

### Using curl

```bash
curl http://localhost:3000/api/Portfolio/user/alice
```

### Expected Response

```json
{
  "id": "12345abcdef67890",
  "userId": "67890abcdef12345",
  "username": "alice",
  "stocks": [],
  "totalValueUsd": 0,
  "lastUpdated": "2025-01-15T10:30:00Z"
}
```

The portfolio is currently empty (no stocks).

## Step 5: Add Stocks to Portfolio

Now add stocks to Alice's portfolio using the stock name or alias.

### Using Swagger UI

1. Find **POST /api/Portfolio/{id}/stocks**
2. Click **"Try it out"**
3. Enter portfolio ID: `12345abcdef67890`
4. Enter request body:

```json
{
  "stockIdentifier": "AAPL",
  "quantity": 10
}
```

5. Click **"Execute"**

### Using curl

```bash
curl -X POST http://localhost:3000/api/Portfolio/12345abcdef67890/stocks \
  -H "Content-Type: application/json" \
  -d '{
    "stockIdentifier": "AAPL",
    "quantity": 10
  }'
```

### Add More Stocks

Repeat the process to add more stocks:

```bash
# Add Tesla stock
curl -X POST http://localhost:3000/api/Portfolio/12345abcdef67890/stocks \
  -H "Content-Type: application/json" \
  -d '{
    "stockIdentifier": "TSLA",
    "quantity": 5
  }'

# Add Taiwan Semiconductor
curl -X POST http://localhost:3000/api/Portfolio/12345abcdef67890/stocks \
  -H "Content-Type: application/json" \
  -d '{
    "stockIdentifier": "2330.TW",
    "quantity": 100
  }'
```

## Step 6: View Updated Portfolio

Retrieve the portfolio again to see the added stocks with full details.

```bash
curl http://localhost:3000/api/Portfolio/user/alice
```

### Expected Response

```json
{
  "id": "12345abcdef67890",
  "userId": "67890abcdef12345",
  "username": "alice",
  "stocks": [
    {
      "stockId": "stock-id-1",
      "quantity": 10,
      "name": "Apple Inc.",
      "alias": "AAPL",
      "price": 150.25,
      "currency": "USD",
      "totalValue": 1502.50
    },
    {
      "stockId": "stock-id-2",
      "quantity": 5,
      "name": "Tesla Inc.",
      "alias": "TSLA",
      "price": 245.80,
      "currency": "USD",
      "totalValue": 1229.00
    },
    {
      "stockId": "stock-id-3",
      "quantity": 100,
      "name": "Taiwan Semiconductor",
      "alias": "2330.TW",
      "price": 550.00,
      "currency": "TWD",
      "totalValue": 55000.00
    }
  ],
  "totalValueUsd": 4476.98,
  "lastUpdated": "2025-01-15T11:00:00Z"
}
```

## Step 7: Update Stock Quantity

Modify the quantity of a stock in the portfolio.

### Using Swagger UI

1. Find **PUT /api/Portfolio/{id}/stocks/{stockId}**
2. Click **"Try it out"**
3. Enter portfolio ID: `12345abcdef67890`
4. Enter stock ID: `stock-id-1`
5. Enter query parameter `quantity`: `15`
6. Click **"Execute"**

### Using curl

```bash
curl -X PUT "http://localhost:3000/api/Portfolio/12345abcdef67890/stocks/stock-id-1?quantity=15"
```

This updates the AAPL quantity from 10 to 15 shares.

## Step 8: View Portfolio Daily Values

After the scheduled job (`RecordDailyValueJob`) runs, you can view historical portfolio values.

**Note**: The job runs on weekdays at 13:35 and Saturdays at 05:35 (Asia/Taipei time). For testing, you may need to wait or manually trigger the job.

### Get Portfolio History

```bash
curl "http://localhost:3000/api/PortfolioDailyValue/12345abcdef67890/history?range=OneMonth"
```

### Expected Response

```json
{
  "portfolioId": "12345abcdef67890",
  "range": "OneMonth",
  "data": [
    {
      "date": "2025-01-14T00:00:00Z",
      "totalValueTwd": 140000.00
    },
    {
      "date": "2025-01-15T00:00:00Z",
      "totalValueTwd": 145250.00
    }
  ],
  "count": 2
}
```

### Get Portfolio Summary

```bash
curl "http://localhost:3000/api/PortfolioDailyValue/12345abcdef67890/summary?range=OneMonth"
```

### Expected Response

```json
{
  "portfolioId": "12345abcdef67890",
  "range": "OneMonth",
  "currentValue": 145250.00,
  "startValue": 140000.00,
  "minValue": 138500.00,
  "maxValue": 148000.00,
  "averageValue": 142375.00,
  "change": 5250.00,
  "changePercent": 3.75,
  "dataPoints": 30,
  "startDate": "2024-12-15T00:00:00Z",
  "endDate": "2025-01-15T00:00:00Z"
}
```

## Step 9: Update Stock Prices

Update stock prices as market conditions change.

```bash
curl -X PUT "http://localhost:3000/api/Stock/name/AAPL/price?price=155.50"
```

This updates the price of AAPL stock, which will be reflected in portfolio valuations.

## Step 10: Remove Stock from Portfolio

Remove a stock from the portfolio when no longer held.

### Using curl

```bash
curl -X DELETE http://localhost:3000/api/Portfolio/12345abcdef67890/stocks/stock-id-2
```

This removes Tesla (TSLA) from Alice's portfolio.

## Recap

Congratulations! You've learned how to:

1. ✅ Create a user (which auto-creates a portfolio)
2. ✅ Add stocks to a portfolio
3. ✅ View enriched portfolio with stock details
4. ✅ Update stock quantities
5. ✅ View historical daily values and summaries
6. ✅ Update stock prices
7. ✅ Remove stocks from a portfolio

## Next Steps

- **Explore Exchange Rates**: Learn how currency conversion works in [Exchange Rate Guide](../guides/exchange-rates.md)
- **Background Jobs**: Understand how daily value recording works in [Architecture Overview](../guides/architecture.md#background-jobs-quartznet)
- **API Reference**: See full API documentation in [API Reference](../api/README.md)

## Troubleshooting

### "Stock not found" error

**Problem**: When adding a stock to a portfolio, you receive "Stock not found".

**Solution**: Ensure the stock exists in the database. Stocks must be added before they can be included in portfolios.

### "User already exists" error

**Problem**: Username or email is already taken.

**Solution**: Use a different username or email, or delete the existing user first.

### Portfolio value not updating

**Problem**: Daily values are not being recorded.

**Solution**: 
- Check that the Quartz scheduler is running
- Verify the scheduled job times in application logs
- Ensure the ExchangeRateService can fetch rates
- Check MongoDB connection for write permissions

---

Happy portfolio management! 🚀
