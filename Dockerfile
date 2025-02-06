FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["PortfolioManager.csproj", "./"]

# 強制安裝特定版本的套件
RUN dotnet add package Microsoft.Extensions.Diagnostics --version 9.0.0
RUN dotnet add package Microsoft.Extensions.Diagnostics.Abstractions --version 9.0.0

RUN dotnet restore
COPY . .

# 優化發布設定 (針對專案檔案而非解決方案)
RUN dotnet publish PortfolioManager.csproj -c Release -o /app/publish \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true \
    /p:DebugType=None \
    /p:DebugSymbols=false \
    /p:TargetFramework=net9.0 \
    /p:RuntimeIdentifier=linux-musl-x64

# 使用 Alpine 基底映像以減少大小
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build /app/publish .

# 移除 global.json（如果有被複製進來的話）
RUN if [ -f global.json ]; then rm global.json; fi

# 設置環境變數
ARG PORT=80
ENV ASPNETCORE_URLS=http://+:${PORT} \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1 \
    DOTNET_GCHighMemPercent=90 \
    DOTNET_MaxRAMPercentage=80 \
    DOTNET_DiagnosticPorts="" \
    DOTNET_DiagnosticPortOptions="" \
    DOTNET_StringLiteralInterningPolicy=High

# 指定 ENTRYPOINT 並添加 GC 優化參數
ENTRYPOINT ["dotnet", "PortfolioManager.dll", "--no-metrics", "--gc-concurrent"]
