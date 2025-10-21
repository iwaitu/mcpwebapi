using McpDotNet.Client;
using McpDotNet.Configuration;
using McpDotNet.Protocol.Transport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using System.Text.Encodings.Web;
using System.Text.Json;



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
                    Location = "http://localhost:5214/sse",
                };
                McpClientOptions clientOptions = new()
                {
                    ClientInfo = new() { Name = "SimpleToolsConsole", Version = "1.0.0" }
                };
                var mcpClient = await McpClientFactory.CreateAsync(serverConfig, clientOptions, cancellationToken: cancellationToken);
                var tools = await mcpClient.GetAIFunctionsAsync(cancellationToken);

                var messages = new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.User, "����ʲô������")
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
                // ������ VLLM/OpenAI ���� API ���� 404/����ҳ��
                _logger.LogError(ex, "ChatTest ����ʧ�ܣ����ܵ� VLLM �˵����ô�������粻�ɴ");
                return Problem(title: "������������쳣", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatTest δԤ���쳣��");
                return Problem(title: "�������ڲ�����", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
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
                    new ChatMessage(ChatRole.System ,"����һ���������֣����ֽзƷ�"),
                    new ChatMessage(ChatRole.User,"ִ����δ��룺\nprint('result=',2**3)")
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
                // ������ VLLM/OpenAI ���� API ���� 404/����ҳ��
                _logger.LogError(ex, "ChatTest ����ʧ�ܣ����ܵ� VLLM �˵����ô�������粻�ɴ");
                return Problem(title: "������������쳣", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatTest δԤ���쳣��");
                return Problem(title: "�������ڲ�����", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        [HttpPost("codeteststream")]
        public async Task<IActionResult> ChatTest3(CancellationToken cancellationToken)
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
                var _ = tools[0].JsonSchema.ToString();

                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System ,"����һ���������֣����ֽзƷ�"),
                    new ChatMessage(ChatRole.User,"ִ����δ��룺\nprint('result=',2**3)")
                };

                var chatOptions = new ChatOptions
                {
                    Temperature = 0.5f,
                    Tools = tools.ToArray()
                };

                // �����ֵ䣺toolName -> ����ʵ��(argsJson) ���� ֱ�ӵ��ù��߰�װ�� InvokeAsync���Զ���΢�� HostedMcpServerTool ������
                var toolInvokers = new Dictionary<string, Func<string, CancellationToken, Task<string>>>();
                foreach (var t in tools)
                {
                    var toolName = t.Name;
                    toolInvokers[toolName] = async (argsJson, ct) =>
                    {
                        try
                        {
                            // ֱ���߹��߰�װ��Զ�� MCP ���ߵĴ���
                            var dynTool = (dynamic)t;
                            var result = await dynTool.InvokeAsync(argsJson, ct);
                            return result?.ToString() ?? string.Empty;
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "�ֶ����ù���ʧ��: {tool}", toolName);
                            return string.Empty;
                        }
                    };
                }

                // �ռ����ù��Ĺ��ߣ�����֪ͨǰ��
                var invokedTools = new List<object>();

                while (true)
                {
                    var assistantDeltaContents = new List<AIContent>();
                    List<FunctionCallContent>? toolCalls = null;

                    await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken))
                    {
                        if (update.Contents?.Count > 0)
                        {
                            assistantDeltaContents.AddRange(update.Contents);

                            // ����ǰ��ʵʱ��ʾ������������ǰ�����������ı�
                            foreach (var textPart in update.Contents.OfType<TextContent>())
                            {
                                _logger.LogInformation("stream: {text}", textPart.Text);
                            }
                        }

                        if (update.FinishReason == ChatFinishReason.ToolCalls)
                        {
                            toolCalls = update.Contents.OfType<FunctionCallContent>().ToList();
                            break;
                        }
                    }

                    // �������������õ� Assistant ���ݼ�����ʷ
                    if (assistantDeltaContents.Count > 0)
                    {
                        var assistantMsg = new ChatMessage(ChatRole.Assistant, string.Empty);
                        foreach (var c in assistantDeltaContents)
                        {
                            assistantMsg.Contents.Add(c);
                        }
                        messages.Add(assistantMsg);
                    }

                    // ���û�й��ߵ��ã�˵��ģ���Ѹ������մ�
                    if (toolCalls == null || toolCalls.Count == 0)
                    {
                        var finalText = string.Join(string.Empty, assistantDeltaContents.OfType<TextContent>().Select(t => t.Text));
                        return Ok(new { tools = invokedTools, answer = finalText });
                    }

                    // �ֶ�ִ�й��ߣ����ֵ��ҵ���Ӧ������ִ�У�������� Tool ��ɫ���ظ�ģ��
                    foreach (var fc in toolCalls)
                    {
                        var argsJson = fc.Arguments?.ToString() ?? string.Empty;

                        invokedTools.Add(new { name = fc.Name, arguments = argsJson });
                        _logger.LogInformation("׼�����ù���: {name}, args: {args}", fc.Name, argsJson);

                        string toolResult = string.Empty;
                        if (toolInvokers.TryGetValue(fc.Name, out var invoker))
                        {
                            toolResult = await invoker(argsJson, cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning("δ�ҵ�����: {name}", fc.Name);
                            toolResult = $"δ�ҵ�����: {fc.Name}";
                        }

                        var toolMsg = new ChatMessage(ChatRole.Tool, string.Empty);
                        toolMsg.Contents.Add(new FunctionResultContent(fc.Name, toolResult));
                        messages.Add(toolMsg);
                    }

                    // �ֶ����ע����Ϻ��ٴ�����ģ�ͣ���ȡ�����ܵģ����ջظ�
                    var followup = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                    messages.Add(followup.Messages[0]);

                    while (followup.FinishReason == ChatFinishReason.ToolCalls)
                    {
                        var moreCalls = followup.Messages[0].Contents.OfType<FunctionCallContent>().ToList();
                        foreach (var fc in moreCalls)
                        {
                            var argsJson = fc.Arguments?.ToString() ?? string.Empty;

                            invokedTools.Add(new { name = fc.Name, arguments = argsJson });
                            _logger.LogInformation("׼�����ù���: {name}, args: {args}", fc.Name, argsJson);

                            string toolResult = string.Empty;
                            if (toolInvokers.TryGetValue(fc.Name, out var invoker))
                            {
                                toolResult = await invoker(argsJson, cancellationToken);
                            }
                            else
                            {
                                _logger.LogWarning("δ�ҵ�����: {name}", fc.Name);
                                toolResult = $"δ�ҵ�����: {fc.Name}";
                            }

                            var toolMsg = new ChatMessage(ChatRole.Tool, string.Empty);
                            toolMsg.Contents.Add(new FunctionResultContent(fc.Name, toolResult));
                            messages.Add(toolMsg);
                        }

                        followup = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                        messages.Add(followup.Messages[0]);
                    }

                    var final = followup.Text ?? string.Empty;
                    return Ok(new { tools = invokedTools, answer = final });
                }
            }
            catch (InvalidOperationException ex)
            {
                // ������ VLLM/OpenAI ���� API ���� 404/����ҳ��
                _logger.LogError(ex, "ChatTest ����ʧ�ܣ����ܵ� VLLM �˵����ô�������粻�ɴ");
                return Problem(title: "������������쳣", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatTest δԤ���쳣��");
                return Problem(title: "�������ڲ�����", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
