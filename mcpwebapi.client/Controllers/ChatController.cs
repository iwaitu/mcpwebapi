using McpDotNet.Client;
using McpDotNet.Configuration;

using McpDotNet.Protocol.Transport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;



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
        public async Task<IActionResult> ChatTest(CancellationToken cancellationToken)
        {
            try
            {
                McpServerConfig serverConfig = new()
                {
                    Id = "everything",
                    Name = "Everything",
                    TransportType = TransportTypes.Sse,
                    Location = "https://mcp-ci.nngeo.net/sse",
                };
                McpClientOptions clientOptions = new()
                {
                    ClientInfo = new() { Name = "SimpleToolsConsole", Version = "1.0.0" }
                };
                var mcpClient = await McpClientFactory.CreateAsync(serverConfig, clientOptions, cancellationToken: cancellationToken);
                var tools = await mcpClient.GetAIFunctionsAsync(cancellationToken);

                var messages = new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.User, "你有什么能力？")
                    };
                var chatOptions = new ChatOptions
                {
                    Temperature = 0.5f,
                    Tools = tools.ToArray()
                };
               
                var res = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                var text = res.Messages.LastOrDefault()?.Text;
                return Ok(text ?? string.Empty);
            }
            catch (InvalidOperationException ex)
            {
                // 典型于 VLLM/OpenAI 兼容 API 返回 404/错误页面
                _logger.LogError(ex, "ChatTest 调用失败，可能的 VLLM 端点配置错误或网络不可达。");
                return Problem(title: "上游聊天服务异常", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatTest 未预期异常。");
                return Problem(title: "服务器内部错误", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("codetest")]
        public async Task<IActionResult> ChatTest2(CancellationToken cancellationToken)
        {
            try
            {
                McpServerConfig serverConfig = new()
                {
                    Id = "everything",
                    Name = "Everything",
                    TransportType = TransportTypes.Sse,
                    Location = "http://localhost:3002/sse",
                };
                McpClientOptions clientOptions = new()
                {
                    ClientInfo = new() { Name = "SimpleToolsConsole", Version = "1.0.0" }
                };
                var mcpClient = await McpClientFactory.CreateAsync(serverConfig, clientOptions, cancellationToken: cancellationToken);
                var tools = await mcpClient.GetAIFunctionsAsync(cancellationToken);
                var test = tools[0].JsonSchema.ToString();
                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System ,"你是一个智能助手，名字叫菲菲"),
                    new ChatMessage(ChatRole.User,"执行这段代码：\nprint('result=',2**3)")
                };
                var chatOptions = new ChatOptions
                {
                    Temperature = 0.5f,
                    Tools = tools.ToArray()
                };

                var res = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                if(res.FinishReason == ChatFinishReason.ToolCalls)
                {
                    var functionCalls = res.Messages[0].Contents.OfType<FunctionCallContent>().ToList();

                }

                return Ok(res.Text);
            }
            catch (InvalidOperationException ex)
            {
                // 典型于 VLLM/OpenAI 兼容 API 返回 404/错误页面
                _logger.LogError(ex, "ChatTest 调用失败，可能的 VLLM 端点配置错误或网络不可达。");
                return Problem(title: "上游聊天服务异常", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatTest 未预期异常。");
                return Problem(title: "服务器内部错误", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
