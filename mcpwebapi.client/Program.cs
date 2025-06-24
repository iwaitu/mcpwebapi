using McpDotNet.Client;
using McpDotNet.Configuration;
using McpDotNet.Protocol.Transport;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var chatclient = new VllmQwen3ChatClient(
               builder.Configuration["Vllm:ApiUrl"],
               builder.Configuration["Vllm:ApiKey"],
               builder.Configuration["Vllm:Model"]);

IChatClient chatClient = new ChatClientBuilder(chatclient)
            .UseFunctionInvocation()
            .Build();

builder.Services.AddScoped(sp => chatClient);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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



