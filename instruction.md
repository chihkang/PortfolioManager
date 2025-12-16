# instruction.md

這份文件面向「開發 / 部署 / 維運」，內容以本專案現況為準。

## 1) 環境變數（Production/Zeabur 建議）

### 必要

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://0.0.0.0:8080`
- `MongoDbSettings__ConnectionString=<mongodb connection string>`
- `MongoDbSettings__DatabaseName=<db name>`

### 相容（若你既有環境已使用）

程式也接受：

- `MongoSettings__ConnectionString`
- `MongoSettings__DatabaseName`

> 建議逐步遷移到 `MongoDbSettings__*`，更符合 ASP.NET Core Options 綁定慣例。

### 可選

- `TZ=Asia/Taipei`：讓容器內時區一致（Quartz cron 仍以 Asia/Taipei 的 TimeZoneInfo 執行）。

## 2) Zeabur 常見問題排查

### 502 Bad Gateway

通常代表「反代打得到容器，但容器服務沒正確回應」，高機率原因：

- **Port mismatch**：平台打 8080，但 app 只聽 3000/80
  - 解法：平台設定 `ASPNETCORE_URLS=http://0.0.0.0:8080`
  - 解法：避免在程式碼內強制 `UseUrls(...)` 覆蓋平台設定

- **App crash / restart loop**：容器一直重啟
  - 解法：看 Zeabur logs 的 exception stack trace
  - 常見根因：MongoDB 連線變數未設定或連線失敗

### HTTPS redirection 警告

若 logs 出現：

- `Failed to determine the https port for redirect.`

表示 TLS 在反代終結，應信任 `X-Forwarded-Proto`。
本專案已設定 Forwarded Headers + `HttpsRedirectionOptions.HttpsPort = 443`，重新部署後該警告應消失。

## 3) 本地開發流程

### 啟動

```bash
dotnet run --project PortfolioManager.csproj
```

- Swagger：`http://localhost:3000/swagger`

### 建置

```bash
dotnet build PortfolioManager.sln
```

### 常用測試呼叫

- `GET http://localhost:3000/api/User`
- `GET http://localhost:3000/swagger`

## 4) Docker 操作

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

## 5) MongoDB 初始化行為（重要）

`MongoDbService` 在啟動時會：

- `ping` MongoDB 驗證連線
- 讀取 dbStats / collStats（logs 會非常長）
- 建立 index（帶重試）

注意事項：

- 初次部署可能會比一般 API 慢一點（取決於 Atlas latency / index 建立速度）。
- 若不希望 production logs 太吵，建議後續把 `collStats` 這類詳細輸出改成只在 Development 印。

## 6) 排程（Quartz）

- 使用 Asia/Taipei 時區的 Cron
  - 週一至週五 13:35
  - 週六 05:35

如果平台會休眠/縮容，排程可能不會準時觸發；需確保服務常駐或改用外部 scheduler。
