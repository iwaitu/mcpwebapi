using McpDotNet;
using McpDotNet.Configuration;
using McpDotNet.Server;
using McpDotNet.Webapi;
using System.ComponentModel;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddMcpServer()
    .WithHttpListenerSseServerTransport("testserver", 3500);
    //.WithApiFunctions()
    //.WithTools(); // 扫描当前程序集中标记了 [McpTool] 的方法并注册为工具&#8203;:contentReference[oaicite:2]{index=2}&#8203;:contentReference[oaicite:3]{index=3};

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 将 API 控制器中的所有 public 且返回类型不是IActionResult 方法添加为工具
await app.UseMcpApiFunctions(typeof(Program).Assembly);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();


[McpToolType]
public static class EchoTool
{
    // 定义一个简单的“回声”工具方法，将传入的 message 原样返回（添加前缀）。
    [McpTool, Description("Echoes the message back to the client.")]
    public static string Echo([Description("the text need to be echo")] string message)
    {
        // 工具逻辑：返回回声消息
        return "hello " + message;
    }
}