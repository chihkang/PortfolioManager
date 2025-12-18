# Getting Started with PortfolioManager

PortfolioManager is a .NET 10 Web API application for managing investment portfolios, built with MongoDB and Quartz.NET for scheduled tasks.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) (pre-release version as specified in `global.json`)
- [MongoDB](https://www.mongodb.com/) (MongoDB Atlas or self-hosted instance)
- (Optional) [Docker](https://www.docker.com/) for containerized deployment

## Local Development Setup

### 1. Clone the Repository

```bash
git clone https://github.com/chihkang/PortfolioManager.git
cd PortfolioManager
```

### 2. Configure MongoDB Connection

Set environment variables for MongoDB connection. Choose one of the following methods:

**Option A: Using .NET User Secrets (Recommended for Development)**

```bash
dotnet user-secrets set "MongoDbSettings:ConnectionString" "mongodb://localhost:27017"
dotnet user-secrets set "MongoDbSettings:DatabaseName" "portfolio_db"
```

**Option B: Using Environment Variables**

```bash
# Windows (PowerShell)
$env:MongoDbSettings__ConnectionString = "mongodb://localhost:27017"
$env:MongoDbSettings__DatabaseName = "portfolio_db"

# Linux/macOS
export MongoDbSettings__ConnectionString="mongodb://localhost:27017"
export MongoDbSettings__DatabaseName="portfolio_db"
```

**Legacy Naming Support**: The application also supports the older `MongoSettings__*` prefix for backward compatibility.

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Run the Application

```bash
dotnet run --project PortfolioManager.csproj
```

The application will start on `http://localhost:3000` (configured in `Properties/launchSettings.json`).

### 5. Access Swagger UI

Open your browser and navigate to:

```
http://localhost:3000/swagger
```

The Swagger UI provides interactive API documentation and testing capabilities.

## Docker Deployment

### Build the Docker Image

```bash
docker build -t portfoliomanager .
```

### Run the Container

```bash
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_URLS=http://0.0.0.0:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e MongoDbSettings__ConnectionString="mongodb+srv://username:password@cluster.mongodb.net/" \
  -e MongoDbSettings__DatabaseName="portfolio_db" \
  portfoliomanager
```

Access the application at `http://localhost:8080/swagger`.

## Testing

### Run All Tests

```bash
dotnet test PortfolioManager.sln
```

### Generate Code Coverage Report

```bash
dotnet test PortfolioManager.sln -c Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

Coverage reports are generated in Cobertura format in the `./TestResults` directory.

## Next Steps

- **Create Your First User**: See [Creating a User Tutorial](../tutorials/creating-user.md)
- **Explore the API**: Check out the [API Reference](../api/README.md)
- **Deploy to Production**: Read the [Deployment Guide](./deployment.md)
- **Understand the Architecture**: See [Architecture Overview](./architecture.md)

## Common Issues

### Connection Refused to MongoDB

**Problem**: Application fails to start with MongoDB connection errors.

**Solution**: 
- Verify MongoDB is running: `mongosh` or check MongoDB Atlas connection
- Ensure connection string is correctly formatted
- Check firewall rules allow connections to MongoDB port (27017)

### Port Already in Use

**Problem**: Cannot start application because port 3000/8080 is in use.

**Solution**:
- Change port in `Properties/launchSettings.json` (Development)
- Or set `ASPNETCORE_URLS` environment variable (Production)

```bash
# Use a different port
dotnet run --urls "http://localhost:5000"
```

### 502 Bad Gateway (Docker/Zeabur)

**Problem**: Application is unreachable through reverse proxy.

**Solution**:
- Ensure `ASPNETCORE_URLS` matches the container's listening port
- Verify the platform's port forwarding configuration
- Check that `ForwardedHeaders` middleware is configured (already done in `Program.cs`)

For more troubleshooting, see the root [README.md](../../README.md#troubleshooting).
