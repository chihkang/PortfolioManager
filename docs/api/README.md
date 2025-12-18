# API Reference

PortfolioManager provides a RESTful API for managing users, portfolios, stocks, and daily value tracking.

## Base URL

- **Development**: `http://localhost:3000/api`
- **Production**: `https://your-domain.com/api`

## Authentication

Currently, the API does not require authentication. **This should be implemented before production use.**

## Interactive Documentation

Access Swagger UI for interactive API testing:
- Development: `http://localhost:3000/swagger`
- Production: `https://your-domain.com/swagger`

## API Endpoints Overview

| Resource | Endpoints | Description |
|----------|-----------|-------------|
| **User** | [User API](#user-api) | User account management |
| **Portfolio** | [Portfolio API](#portfolio-api) | Portfolio operations |
| **Stock** | [Stock API](#stock-api) | Stock data management |
| **Exchange Rate** | [Exchange Rate API](#exchange-rate-api) | Currency exchange rates |
| **Daily Value** | [Daily Value API](#daily-value-api) | Historical portfolio values |

---

## User API

### Get All Users

Retrieve a list of all users.

**Endpoint**: `GET /api/User`

**Response**: `200 OK`

```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "username": "john_doe",
    "email": "john@example.com",
    "portfolioId": "507f1f77bcf86cd799439012",
    "createdAt": "2025-01-15T10:30:00Z"
  }
]
```

---

### Create User

Create a new user. Automatically creates an associated portfolio.

**Endpoint**: `POST /api/User`

**Request Body**:

```json
{
  "username": "john_doe",
  "email": "john@example.com"
}
```

**Response**: `201 Created`

```json
{
  "id": "507f1f77bcf86cd799439011",
  "username": "john_doe",
  "email": "john@example.com",
  "portfolioId": "507f1f77bcf86cd799439012",
  "createdAt": "2025-01-15T10:30:00Z"
}
```

---

## Portfolio API

### Get Enriched Portfolio by Username

Retrieve a portfolio with full stock details by username.

**Endpoint**: `GET /api/Portfolio/user/{username}`

**Parameters**:
- `username` (string, required): Username

**Response**: `200 OK`

```json
{
  "id": "507f1f77bcf86cd799439012",
  "userId": "507f1f77bcf86cd799439011",
  "username": "john_doe",
  "stocks": [
    {
      "stockId": "507f1f77bcf86cd799439013",
      "quantity": 10,
      "name": "Apple Inc.",
      "alias": "AAPL",
      "price": 150.25,
      "currency": "USD",
      "totalValue": 1502.50
    }
  ],
  "totalValueUsd": 1502.50,
  "lastUpdated": "2025-01-15T10:30:00Z"
}
```

---

### Add Stock to Portfolio

Add a stock to a portfolio using stock name or alias.

**Endpoint**: `POST /api/Portfolio/{id}/stocks`

**Parameters**:
- `id` (string, required): Portfolio ID

**Request Body**:

```json
{
  "stockIdentifier": "AAPL",
  "quantity": 10
}
```

**Response**: `200 OK`

---

### Update Stock Quantity

Update the quantity of a stock in a portfolio.

**Endpoint**: `PUT /api/Portfolio/{id}/stocks/{stockId}`

**Parameters**:
- `id` (string, required): Portfolio ID
- `stockId` (string, required): Stock ID

**Query Parameters**:
- `quantity` (integer, required): New quantity

**Response**: `200 OK`

---

## Stock API

### Get All Stocks

Retrieve all stocks. Results are cached for 5 minutes.

**Endpoint**: `GET /api/Stock`

**Response**: `200 OK`

```json
[
  {
    "id": "507f1f77bcf86cd799439013",
    "name": "Apple Inc.",
    "alias": "AAPL",
    "price": 150.25,
    "currency": "USD",
    "lastUpdated": "2025-01-15T10:30:00Z"
  }
]
```

---

### Update Stock Price

Update the price of a stock by name.

**Endpoint**: `PUT /api/Stock/name/{name}/price`

**Parameters**:
- `name` (string, required): Stock name or alias

**Query Parameters**:
- `price` (decimal, required): New price

**Response**: `200 OK`

---

## Exchange Rate API

### Get Exchange Rate

Fetch the current exchange rate for a currency pair.

**Endpoint**: `GET /api/ExchangeRate/{currencyPair}`

**Parameters**:
- `currencyPair` (string, optional): Currency pair (e.g., `USD-TWD`). Defaults to `USD-TWD`.

**Response**: `200 OK`

```json
{
  "currencyPair": "USD-TWD",
  "rate": 31.50,
  "timestamp": "2025-01-15T10:30:00Z"
}
```

---

## Daily Value API

### Get Portfolio History

Retrieve historical daily values for a portfolio.

**Endpoint**: `GET /api/PortfolioDailyValue/{portfolioId}/history`

**Parameters**:
- `portfolioId` (string, required): Portfolio ID

**Query Parameters**:
- `range` (enum, optional): Time range
  - `OneMonth` (default)
  - `ThreeMonths`
  - `SixMonths`
  - `OneYear`

**Response**: `200 OK`

```json
{
  "portfolioId": "507f1f77bcf86cd799439012",
  "range": "OneMonth",
  "data": [
    {
      "date": "2025-01-14T00:00:00Z",
      "totalValueTwd": 45000.00
    },
    {
      "date": "2025-01-15T00:00:00Z",
      "totalValueTwd": 47250.00
    }
  ],
  "count": 2
}
```

---

## Error Responses

All endpoints follow standard HTTP status codes:

| Code | Meaning | Description |
|------|---------|-------------|
| `200` | OK | Request succeeded |
| `201` | Created | Resource created successfully |
| `204` | No Content | Request succeeded with no response body |
| `400` | Bad Request | Invalid input data |
| `404` | Not Found | Resource not found |
| `409` | Conflict | Resource conflict (e.g., duplicate username) |
| `500` | Internal Server Error | Server-side error |

---

## Further Reading

- [Getting Started Guide](../guides/getting-started.md)
- [Tutorials](../tutorials/)
- [Architecture Overview](../guides/architecture.md)
