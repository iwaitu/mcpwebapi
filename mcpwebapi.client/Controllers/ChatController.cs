using McpDotNet.Client;
using McpDotNet.Configuration;

using McpDotNet.Protocol.Transport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;



namespace mcpwebapi.client.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {

        private readonly ILogger<ChatController> _logger;
        private readonly IChatClient _chatClient;

        public ChatController(ILogger<ChatController> logger,IChatClient client)
        {
            _logger = logger;
            _chatClient = client;
        }




        [HttpPost]
        public async Task<string> ChatTest()
        {
            McpServerConfig serverConfig = new()
            {
                Id = "everything",
                Name = "Everything",
                TransportType = TransportTypes.Sse,
                Location = "http://localhost:3500/sse",
            };
            McpClientOptions clientOptions = new()
            {
                ClientInfo = new() { Name = "SimpleToolsConsole", Version = "1.0.0" }
            };
            var mcpClient = await McpClientFactory.CreateAsync(serverConfig, clientOptions);
            var tools = await mcpClient.GetAIFunctionsAsync();

            var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.User, "南宁的天气如何？")
                };
            var chatOptions = new ChatOptions
            {
                Temperature = 0.5f,
                Tools = tools.ToArray()
            };
           
            var res = await _chatClient.GetResponseAsync(messages, chatOptions);
            return res.Messages.LastOrDefault()?.Text;
        }
    }
}
