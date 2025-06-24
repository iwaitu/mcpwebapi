# mcp webapi demo

本仓库包含两个 .NET 8 项目：`mcpwebapi`（服务端 WebAPI）和 `mcpwebapi.client`（客户端 WebAPI）。

## 项目结构

- **mcpwebapi**：提供天气查询等 API。
- **mcpwebapi.client**：提供聊天接口，演示 AI 聊天功能。

## 主要功能

### mcpwebapi
- 天气查询接口（`WeatherForecastController`）

### mcpwebapi.client
- 聊天接口（`ChatController`），集成 AI 聊天能力

## 运行方式

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. 在项目根目录下运行：
   ```bash
   dotnet build
   dotnet run --project mcpwebapi/mcpwebapi.csproj
   dotnet run --project mcpwebapi.client/mcpwebapi.client.csproj
   ```
3. 访问相应的 API 端点进行测试。

## 依赖说明
- Ivilson.McpDotNet.Webapi
- Ivilson.McpDotNet.Client
- Microsoft.AspNetCore

## 备注
如需自定义配置，请参考各项目下的 `appsettings.json`。
