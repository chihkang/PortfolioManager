FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["PortfolioManager.csproj", "./"]

# 強制安裝特定版本的套件
RUN dotnet add package Microsoft.Extensions.Diagnostics --version 9.0.0
RUN dotnet add package Microsoft.Extensions.Diagnostics.Abstractions --version 9.0.0

RUN dotnet restore
COPY . .

# 優化發布設定
RUN dotnet publish -c Release -o /app/publish \
    --no-restore \
    /p:PublishTrimmed=true \
    /p:PublishSingleFile=true \
    /p:DebugType=None \
    /p:DebugSymbols=false

# 使用Alpine基底映像以減少大小
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build /app/publish .

# 設置環境變數
ARG PORT=80
ENV ASPNETCORE_URLS=http://+:${PORT} \
    DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_ENVIRONMENT=Production \
    # 啟用 Server GC
    DOTNET_gcServer=1 \
    # 設定 GC 高效能模式
    DOTNET_GCHighMemPercent=90 \
    # 限制記憶體使用
    DOTNET_MaxRAMPercentage=80 \
    # 禁用遙測
    DOTNET_EnableDiagnostics=0 \
    DOTNET_DiagnosticPorts="" \
    DOTNET_DiagnosticPortOptions="" \
    # 優化字串池
    DOTNET_StringLiteralInterningPolicy=High

# 修正ENTRYPOINT格式並加入GC優化參數
ENTRYPOINT ["dotnet", "PortfolioManager.dll", "--no-metrics", "--gc-concurrent"]
