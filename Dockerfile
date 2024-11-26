FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["PortfolioManager.csproj", "./"]

# 強制安裝特定版本的套件
RUN dotnet add package Microsoft.Extensions.Diagnostics --version 8.0.0
RUN dotnet add package Microsoft.Extensions.Diagnostics.Abstractions --version 8.0.0

RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# 設置必要的環境變數
ENV ASPNETCORE_URLS=http://+:${PORT}
ENV DOTNET_EnableDiagnostics=0
ENV DOTNET_DiagnosticPorts=
ENV DOTNET_DiagnosticPortOptions=

# 使用新的啟動命令
ENTRYPOINT ["dotnet", "PortfolioManager.dll", "--no-metrics"]