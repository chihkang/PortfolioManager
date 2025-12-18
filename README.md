# PortfolioManager

一個以 ASP.NET Core (.NET 10) + MongoDB 為主的投資組合（Portfolio）後端 API，提供使用者、投資組合、股票資料與每日資產變化查詢，並內建 Quartz 排程作業。

> 📖 **English Documentation**: See [docs/](./docs/) for comprehensive English guides, tutorials, and API reference.
> 
> 🌐 **Traditional Chinese**: This README (本文件為繁體中文版本)

## Features

- REST API（Controllers）
- MongoDB 儲存（MongoDB.Driver）
- Swagger UI（OpenAPI）
- Quartz 排程（預設使用 RAMJobStore）
- 記憶體快取（MemoryCache / DistributedMemoryCache）

## Requirements

- .NET SDK 10（依 `global.json`；可能為 pre-release）
- MongoDB（Atlas 或自建皆可）

## Run locally

1) 設定環境變數（擇一方式）

- 使用標準 .NET Options 綁定：
  - `MongoDbSettings__ConnectionString`
  - `MongoDbSettings__DatabaseName`

-（相容舊命名）
  - `MongoSettings__ConnectionString`
  - `MongoSettings__DatabaseName`

2) 啟動

```bash
dotnet run --project PortfolioManager.csproj
```

3) 開啟 Swagger

- `http://localhost:3000/swagger`

> 本專案 local profile 預設使用 3000（見 Properties/launchSettings.json）。

## Docker

```bash
docker build -t portfoliomanager .

docker run --rm \
  -p 8080:8080 \
  -e ASPNETCORE_URLS=http://0.0.0.0:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e MongoDbSettings__ConnectionString="<your-mongo-connection-string>" \
  -e MongoDbSettings__DatabaseName="<your-db-name>" \
  portfoliomanager
```

## Deployment notes (Zeabur)

- 建議在平台設定：
  - `ASPNETCORE_URLS=http://0.0.0.0:8080`
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `TZ=Asia/Taipei`（可選）
  - `MongoDbSettings__ConnectionString` / `MongoDbSettings__DatabaseName`（或使用相容的 `MongoSettings__*`）

## API overview

以 `/api` 為前綴（詳細請以 Swagger 為準）：

- `GET /api/User`：取得使用者列表
- `GET /api/User/{id}`：取得使用者
- `POST /api/User`：建立使用者（同時建立 portfolio）
- `PUT /api/User/{id}`：更新使用者
- `DELETE /api/User/{id}`：刪除使用者（含 portfolio）

- `GET /api/Portfolio/{id}`：取得 portfolio
- `POST /api/Portfolio`：建立 portfolio
- `GET /api/Portfolio/user/{username}`：用 username 取得 portfolio（含加值資訊）
- `POST /api/Portfolio/{id}/stocks`：用股票 name/alias 加入 portfolio
- `POST /api/Portfolio/{id}/stocks/byId`：用 stockId 加入 portfolio
- `PUT /api/Portfolio/{id}/stocks/{stockId}`：更新持股數量
- `DELETE /api/Portfolio/{id}/stocks/{stockId}`：移除持股

- `GET /api/Stock`：取得股票列表（含快取）
- `PUT /api/Stock/name/{name}/price`：更新指定股票價格

- `GET /api/ExchangeRate/{currencyPair}`：取得匯率（預設 `USD-TWD`）

- `GET /api/PortfolioDailyValue/{portfolioId}/history?range=OneMonth|ThreeMonths|SixMonths|OneYear`
- `GET /api/PortfolioDailyValue/{portfolioId}/summary?range=...`

## Background jobs (Quartz)

- `RecordDailyValueJob`
  - 週一至週五 13:35（Asia/Taipei）
  - 週六 05:35（Asia/Taipei）

## Testing & Coverage

本專案包含測試專案 `PortfolioManager.Tests`，並在 GitHub Actions 內建 coverage workflow。

本機跑測試：

```bash
dotnet test PortfolioManager.sln
```

本機產生 coverage（Cobertura）：

```bash
dotnet test PortfolioManager.sln -c Release --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

CI（GitHub Actions）：

- Workflow：`.github/workflows/agentic-test-coverage.yml`
- 在 Pull Request 會自動留言顯示：
  - HEAD coverage
  - BASE coverage
  - 差異 $\Delta$（percentage points）
- 可透過 workflow 內的 `COVERAGE_MINIMUM`（0~1，小數）設定最低 coverage gate（預設 0 表示不啟用）

## Troubleshooting

- **502 / 連不上服務**：通常是容器監聽的 port 跟平台對外轉發 port 不一致。請確認平台設定 `ASPNETCORE_URLS`，並避免在程式內硬綁 port。
- **啟動就 crash**：多數是 MongoDB 連線字串或 DB 名稱未設定（startup 會驗證連線並建立 index）。

## Documentation

### 📚 Comprehensive Documentation (English)

- **[Getting Started Guide](./docs/guides/getting-started.md)** - Setup and local development
- **[Architecture Overview](./docs/guides/architecture.md)** - System design and patterns
- **[Deployment Guide](./docs/guides/deployment.md)** - Docker, Zeabur, Kubernetes
- **[API Reference](./docs/api/README.md)** - Complete API documentation
- **[Tutorial: Creating Your First User](./docs/tutorials/creating-user.md)** - Step-by-step guide
- **[Contributing Guidelines](./CONTRIBUTING.md)** - How to contribute

### 🤖 For AI Agents & Developers

- **[AGENT.md](./AGENT.md)** - Domain context for AI agents
- **[.github/copilot-instructions.md](./.github/copilot-instructions.md)** - Technical coding guidelines

---
