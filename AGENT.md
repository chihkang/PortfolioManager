# PortfolioManager Agent Context

This file provides high-level domain knowledge and business logic context for AI agents.
For technical coding guidelines, architecture, and patterns, refer to `.github/copilot-instructions.md`.

## 1. Domain Model & Relationships

The system manages investment portfolios for users, tracking stock holdings and their daily value in TWD.

### Core Entities
- **User**: The account holder.
  - Has one `PortfolioId` (1:1 relationship).
  - Identified by `Username` and `Email`.
- **Portfolio**: The container for investment holdings.
  - Belongs to a `UserId`.
  - Contains a list of `PortfolioStock` items.
- **Stock**: A financial instrument (e.g., AAPL, TSLA).
  - Has `Name`, `Alias` (ticker), `Price`, and `Currency` (USD/TWD).
  - Prices are updated externally or via API.
- **PortfolioStock**: A link between Portfolio and Stock.
  - Stores `Quantity` of a specific `StockId` held in a portfolio.
- **PortfolioDailyValue**: Historical record of a portfolio's total value.
  - Snapshotted daily (or on schedule).
  - Stores `TotalValueTwd` (Total value converted to Taiwan Dollar).

## 2. Business Logic & Workflows

### Portfolio Valuation
- **Calculation**: `Sum(Stock.Price * PortfolioStock.Quantity * ExchangeRate)`.
- **Currency**: All daily values are normalized to **TWD**.
- **Exchange Rate**: Fetched from external service (default `USD-TWD`).

### Daily Recording Job (`RecordDailyValueJob`)
- **Purpose**: Snapshot the total value of all portfolios for historical trending.
- **Schedule**:
  - Weekdays (Mon-Fri) at 13:35 (Asia/Taipei).
  - Saturdays at 05:35 (Asia/Taipei).
- **Process**:
  1. Fetch current `USD-TWD` exchange rate.
  2. Iterate all portfolios.
  3. Calculate total value for each.
  4. Save `PortfolioDailyValue` record.

### User Onboarding
- Creating a `User` automatically creates an empty `Portfolio` for them.
- The `User.PortfolioId` and `Portfolio.UserId` are linked immediately.

## 3. Terminology

- **Enriched Portfolio**: A portfolio object that includes the full `Stock` details (Name, Price) instead of just `StockId`, usually for frontend display.
- **Scraper**: (Implied) Mechanisms to fetch stock prices or exchange rates, though currently handled via `ExchangeRateService` or manual updates.

## 4. Agent Capabilities

When assisting with this project, you can:
- **Refactor**: Convert legacy controllers to Primary Constructors.
- **Optimize**: Suggest `ReadOnlySpan<char>` for ID parsing.
- **Debug**: Check `RecordDailyValueJob` logs for exchange rate failures.
- **Extend**: Add new financial instruments beyond simple Stocks (e.g., ETFs, Crypto) by extending the `Stock` entity or creating new types.
