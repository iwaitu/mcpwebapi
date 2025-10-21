using McpDotNet.Client;
using McpDotNet.Configuration;
using McpDotNet.Protocol.Transport;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);
//var chatclient = new VllmQwen3NextChatClient(
//               builder.Configuration["Vllm:ApiUrl"],
//               builder.Configuration["Vllm:ApiKey"],
//               builder.Configuration["Vllm:Model"]);

var apiKey = Environment.GetEnvironmentVariable("VLLM_API_KEY");


var chatclient = new VllmQwen3NextChatClient("http://localhost:8000/v1/{1}",apiKey,"qwen3-next-80b-a3b-instruct");

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



