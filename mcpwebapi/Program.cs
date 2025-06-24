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
    //.WithTools(); // ɨ�赱ǰ�����б���� [McpTool] �ķ�����ע��Ϊ����&#8203;:contentReference[oaicite:2]{index=2}&#8203;:contentReference[oaicite:3]{index=3};

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// �� API �������е����� public �ҷ������Ͳ���IActionResult �������Ϊ����
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
    // ����һ���򵥵ġ����������߷������������ message ԭ�����أ����ǰ׺����
    [McpTool, Description("Echoes the message back to the client.")]
    public static string Echo([Description("the text need to be echo")] string message)
    {
        // �����߼������ػ�����Ϣ
        return "hello " + message;
    }
}