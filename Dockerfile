FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["PortfolioManager.csproj", "./"]

# 強制安裝特定版本的套件
RUN dotnet add package Microsoft.Extensions.Diagnostics --version 9.0.0
RUN dotnet add package Microsoft.Extensions.Diagnostics.Abstractions --version 9.0.0

RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

# 設置環境變數,使用ARG定義PORT變數
ARG PORT=80
ENV ASPNETCORE_URLS=http://+:${PORT}
ENV DOTNET_EnableDiagnostics="0"
ENV DOTNET_DiagnosticPorts=""
ENV DOTNET_DiagnosticPortOptions=""

# 修正ENTRYPOINT格式
ENTRYPOINT ["dotnet", "PortfolioManager.dll", "--no-metrics"]