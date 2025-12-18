# Deployment Guide

This guide covers deploying PortfolioManager to various environments.

## Table of Contents

- [General Prerequisites](#general-prerequisites)
- [Environment Variables](#environment-variables)
- [Docker Deployment](#docker-deployment)
- [Zeabur Deployment](#zeabur-deployment)
- [Azure App Service](#azure-app-service)
- [Kubernetes](#kubernetes)
- [Post-Deployment Verification](#post-deployment-verification)
- [Troubleshooting](#troubleshooting)

## General Prerequisites

- MongoDB instance (MongoDB Atlas or self-hosted)
- Container registry (Docker Hub, GitHub Container Registry, etc.) for Docker deployments
- Configured environment variables (see below)

## Environment Variables

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `MongoDbSettings__ConnectionString` | MongoDB connection string | `mongodb+srv://user:pass@cluster.mongodb.net/` |
| `MongoDbSettings__DatabaseName` | MongoDB database name | `portfolio_db` |
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Production` |
| `ASPNETCORE_URLS` | Application URLs | `http://0.0.0.0:8080` |

### Optional Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `TZ` | Timezone for scheduled jobs | `UTC` | `Asia/Taipei` |
| `COVERAGE_MINIMUM` | Minimum code coverage (CI) | `0` | `0.75` (75%) |

### Legacy Support

The application supports legacy `MongoSettings__*` prefix for backward compatibility:
- `MongoSettings__ConnectionString`
- `MongoSettings__DatabaseName`

## Docker Deployment

### Build and Push Image

```bash
# Build the image
docker build -t yourusername/portfoliomanager:latest .

# Push to registry
docker push yourusername/portfoliomanager:latest
```

### Run Container

**Basic deployment:**

```bash
docker run -d \
  --name portfoliomanager \
  -p 8080:8080 \
  -e ASPNETCORE_URLS=http://0.0.0.0:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e MongoDbSettings__ConnectionString="mongodb+srv://user:pass@cluster.mongodb.net/" \
  -e MongoDbSettings__DatabaseName="portfolio_db" \
  -e TZ=Asia/Taipei \
  --restart unless-stopped \
  yourusername/portfoliomanager:latest
```

**With Docker Compose:**

```yaml
# docker-compose.yml
version: '3.8'

services:
  portfoliomanager:
    image: yourusername/portfoliomanager:latest
    container_name: portfoliomanager
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_URLS=http://0.0.0.0:8080
      - ASPNETCORE_ENVIRONMENT=Production
      - MongoDbSettings__ConnectionString=${MONGO_CONNECTION_STRING}
      - MongoDbSettings__DatabaseName=${MONGO_DATABASE_NAME}
      - TZ=Asia/Taipei
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/api/User"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

Run with:

```bash
# Create .env file
cat > .env << EOF
MONGO_CONNECTION_STRING=mongodb+srv://user:pass@cluster.mongodb.net/
MONGO_DATABASE_NAME=portfolio_db
EOF

# Start services
docker-compose up -d
```

## Zeabur Deployment

[Zeabur](https://zeabur.com/) provides simple container deployment with automatic HTTPS.

### Step 1: Prepare Your Repository

Ensure your repository contains the `Dockerfile` (already present).

### Step 2: Create a New Project

1. Log in to Zeabur
2. Click **Create New Project**
3. Connect your GitHub repository
4. Select `chihkang/PortfolioManager`

### Step 3: Configure Environment Variables

In the Zeabur dashboard, add environment variables:

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:8080
MongoDbSettings__ConnectionString=mongodb+srv://...
MongoDbSettings__DatabaseName=portfolio_db
TZ=Asia/Taipei
```

### Step 4: Configure Service Settings

- **Port**: 8080 (Zeabur auto-detects from Dockerfile EXPOSE)
- **Health Check Path**: `/api/User` (optional)

### Step 5: Deploy

Click **Deploy**. Zeabur will:
1. Build the Docker image
2. Deploy the container
3. Provide a public URL with HTTPS

### Important Notes for Zeabur

✅ **ForwardedHeaders middleware is already configured** in `Program.cs` to handle Zeabur's reverse proxy.

✅ **No need to configure HTTPS redirection manually** - handled by the platform.

⚠️ **Port mismatch**: Ensure `ASPNETCORE_URLS` matches the container's listening port (8080).

## Post-Deployment Verification

### 1. Check Application Health

```bash
# Replace with your deployment URL
curl https://your-app-url.com/swagger

# Test an API endpoint
curl https://your-app-url.com/api/User
```

### 2. Verify MongoDB Connection

Check application logs for successful MongoDB connection:

```
Initializing MongoDB connection...
Connected to MongoDB successfully
Created indexes for users collection
Created indexes for portfolios collection
```

### 3. Verify Scheduled Jobs

Check logs for Quartz scheduler initialization:

```
Quartz Scheduler started successfully
Next trigger time: 2025-12-18 13:35:00 +08:00
```

### 4. Test API Operations

```bash
# Create a test user
curl -X POST https://your-app-url.com/api/User \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com"
  }'

# Verify user creation
curl https://your-app-url.com/api/User
```

## Troubleshooting

### 502 Bad Gateway

**Symptoms**: Application is unreachable, returns 502 error.

**Causes**:
1. Application not listening on expected port
2. Health check failures
3. Reverse proxy misconfiguration

**Solutions**:
```bash
# Verify ASPNETCORE_URLS matches container port
echo $ASPNETCORE_URLS  # Should be http://0.0.0.0:8080

# Check container logs
docker logs portfoliomanager

# Verify application is listening
docker exec portfoliomanager netstat -tuln | grep 8080
```

### MongoDB Connection Failures

**Symptoms**: Application crashes on startup with MongoDB errors.

**Solutions**:
```bash
# Verify connection string format
# Correct: mongodb+srv://username:password@cluster.mongodb.net/
# Incorrect: Missing credentials, wrong protocol

# Check MongoDB Atlas network access
# Ensure deployment IP is whitelisted (or use 0.0.0.0/0 for testing)
```

### Environment Variables Not Loading

**Symptoms**: Application uses default values instead of configured environment variables.

**Solutions**:
```bash
# Verify environment variables are set
docker exec portfoliomanager printenv | grep Mongo

# Restart application after setting variables
docker restart portfoliomanager
```

## Further Reading

- [Getting Started Guide](./getting-started.md)
- [Architecture Overview](./architecture.md)
- [API Reference](../api/README.md)
