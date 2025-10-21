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
        private const string MCP_ENDPOINT = "http://localhost:5214/sse";
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
                    Location = MCP_ENDPOINT,
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
                    Location = MCP_ENDPOINT,
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
                    new ChatMessage(ChatRole.System ,"你是一个智能助手，名字叫菲菲"),
                    new ChatMessage(ChatRole.User,"执行这段代码：\nprint('result=',2**3)")
                };

                var chatOptions = new ChatOptions
                {
                    Temperature = 0.5f,
                    Tools = tools.ToArray()
                };


                // 工具字典：toolName -> 调用实现(args) —— 直接调用工具包装的 InvokeAsync，以对齐微软 HostedMcpServerTool 的做法
                var toolInvokers = new Dictionary<string, Func<IDictionary<string, object?>, CancellationToken, Task<string>>>();
                foreach (var t in tools)
                {
                    var toolName = t.Name;
                    toolInvokers[toolName] = async (argsDict, ct) =>
                    {
                        try
                        {
                            var aiArgs = new AIFunctionArguments(argsDict);
                            var result = await t.InvokeAsync(aiArgs, ct);
                            if (result is string s) return s;
                            try { return System.Text.Json.JsonSerializer.Serialize(result); } catch { return result?.ToString() ?? string.Empty; }
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "手动调用工具失败: {tool}", toolName);
                            return string.Empty;
                        }
                    };
                }

                // 收集调用过的工具，方便通知前端
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

                            // 如需前端实时显示，可在这里向前端推送增量文本
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

                    // 将包含函数调用的 Assistant 内容加入历史
                    if (assistantDeltaContents.Count > 0)
                    {
                        var assistantMsg = new ChatMessage(ChatRole.Assistant, string.Empty);
                        foreach (var c in assistantDeltaContents)
                        {
                            assistantMsg.Contents.Add(c);
                        }
                        messages.Add(assistantMsg);
                    }

                    // 如果没有工具调用，说明模型已给出最终答案
                    if (toolCalls == null || toolCalls.Count == 0)
                    {
                        var finalText = string.Join(string.Empty, assistantDeltaContents.OfType<TextContent>().Select(t => t.Text));
                        return Ok(new { tools = invokedTools, answer = finalText });
                    }

                    // 手动执行工具：从字典找到对应函数并执行，将结果以 Tool 角色返回给模型
                    foreach (var fc in toolCalls)
                    {
                        if (fc.Arguments is null)
                        {
                            _logger.LogWarning("函数调用缺少参数: {name}", fc.Name);
                            continue;
                        }

                        var argsDisplay = string.Empty;
                        try { argsDisplay = System.Text.Json.JsonSerializer.Serialize(fc.Arguments); } catch { argsDisplay = fc.Arguments.ToString() ?? string.Empty; }

                        invokedTools.Add(new { name = fc.Name, arguments = argsDisplay });
                        _logger.LogInformation("准备调用工具: {name}, args: {args}", fc.Name, argsDisplay);

                        string toolResult = string.Empty;
                        if (toolInvokers.TryGetValue(fc.Name, out var invoker))
                        {
                            toolResult = await invoker(fc.Arguments, cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning("未找到工具: {name}", fc.Name);
                            toolResult = $"未找到工具: {fc.Name}";
                        }

                        var toolMsg = new ChatMessage(ChatRole.Tool, string.Empty);
                        toolMsg.Contents.Add(new FunctionResultContent(fc.Name, toolResult));
                        messages.Add(toolMsg);
                    }

                    // 手动结果注入完毕后，再次请求模型，获取（可能的）最终回复
                    var followup = await _chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                    messages.Add(followup.Messages[0]);

                    while (followup.FinishReason == ChatFinishReason.ToolCalls)
                    {
                        var moreCalls = followup.Messages[0].Contents.OfType<FunctionCallContent>().ToList();
                        foreach (var fc in moreCalls)
                        {
                            if (fc.Arguments is null)
                            {
                                _logger.LogWarning("函数调用缺少参数: {name}", fc.Name);
                                continue;
                            }

                            var argsDisplay = string.Empty;
                            try { argsDisplay = System.Text.Json.JsonSerializer.Serialize(fc.Arguments); } catch { argsDisplay = fc.Arguments.ToString() ?? string.Empty; }

                            invokedTools.Add(new { name = fc.Name, arguments = argsDisplay });
                            _logger.LogInformation("准备调用工具: {name}, args: {args}", fc.Name, argsDisplay);

                            string toolResult = string.Empty;
                            if (toolInvokers.TryGetValue(fc.Name, out var invoker))
                            {
                                toolResult = await invoker(fc.Arguments, cancellationToken);
                            }
                            else
                            {
                                _logger.LogWarning("未找到工具: {name}", fc.Name);
                                toolResult = $"未找到工具: {fc.Name}";
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

        [HttpPost("codeteststream_calltool")]
        public async Task<IActionResult> ChatTest4(CancellationToken cancellationToken)
        {
            try
            {
                // 端点与可选 token
                var endpoint = HttpContext.Request.Query["endpoint"].FirstOrDefault() ?? MCP_ENDPOINT;
                var token = HttpContext.Request.Query["token"].FirstOrDefault();

                var serverConfig = new McpServerConfig
                {
                    Id = "everything-calltool",
                    Name = "Everything (CallTool)",
                    TransportType = TransportTypes.Sse,
                    Location = endpoint,
                };

                // 可选：通过反射设置 Authorization 头（若库支持）
                try
                {
                    var headersProp = serverConfig.GetType().GetProperty("Headers");
                    if (headersProp != null && !string.IsNullOrWhiteSpace(token))
                    {
                        var headers = headersProp.GetValue(serverConfig) as IDictionary<string, string> ?? new Dictionary<string, string>();
                        headers["Authorization"] = $"Bearer {token}";
                        headersProp.SetValue(serverConfig, headers);
                    }
                }
                catch { }

                var clientOptions = new McpClientOptions
                {
                    ClientInfo = new() { Name = "SimpleToolsConsole", Version = "1.0.0" }
                };

                var mcpClient = await McpClientFactory.CreateAsync(serverConfig, clientOptions, cancellationToken: cancellationToken);
                var tools = await mcpClient.GetAIFunctionsAsync(cancellationToken);

                var messages = new List<ChatMessage>
                {
                    new ChatMessage(ChatRole.System ,"你是一个智能助手，名字叫菲菲"),
                    new ChatMessage(ChatRole.User, "执行这段代码：\\nprint('result=',2**3)")
                };

                var chatOptions = new ChatOptions
                {
                    Temperature = 0.3f,
                    Tools = tools.ToArray()
                };

                var invokedTools = new List<object>();
                var finalText = string.Empty;

                while (true)
                {
                    var assistantDeltaContents = new List<AIContent>();
                    List<FunctionCallContent>? toolCalls = null;

                    await foreach (var update in _chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken))
                    {
                        if (update.Contents?.Count > 0)
                        {
                            assistantDeltaContents.AddRange(update.Contents);
                            foreach (var textPart in update.Contents.OfType<TextContent>())
                            {
                                finalText += textPart.Text;
                                _logger.LogInformation("stream: {text}", textPart.Text);
                            }
                        }

                        if (update.FinishReason == ChatFinishReason.ToolCalls)
                        {
                            toolCalls = update.Contents.OfType<FunctionCallContent>().ToList();
                            break;
                        }
                    }

                    if (assistantDeltaContents.Count > 0)
                    {
                        var assistantMsg = new ChatMessage(ChatRole.Assistant, string.Empty);
                        foreach (var c in assistantDeltaContents)
                        {
                            assistantMsg.Contents.Add(c);
                        }
                        messages.Add(assistantMsg);
                    }

                    if (toolCalls == null || toolCalls.Count == 0)
                    {
                        return Ok(new { tools = invokedTools, answer = finalText });
                    }

                    // 通过 mcpClient.CallToolAsync 手动调用远程 MCP 工具
                    foreach (var fc in toolCalls)
                    {
                        var argsDict = new Dictionary<string, object>();
                        if (fc.Arguments != null)
                        {
                            foreach (var kv in fc.Arguments)
                            {
                                argsDict[kv.Key] = kv.Value!;
                            }
                        }

                        var argsDisplay = string.Empty;
                        try { argsDisplay = System.Text.Json.JsonSerializer.Serialize(argsDict); } catch { argsDisplay = argsDict.ToString() ?? string.Empty; }

                        _logger.LogInformation("CallToolAsync -> {name} with args: {args}", fc.Name, argsDisplay);
                        invokedTools.Add(new { name = fc.Name, arguments = argsDisplay });

                        var callRes = await mcpClient.CallToolAsync(fc.Name, argsDict, cancellationToken);
                        var toolResult = string.Empty;
                        try { toolResult = System.Text.Json.JsonSerializer.Serialize(callRes); } catch { toolResult = callRes?.ToString() ?? string.Empty; }

                        var toolMsg = new ChatMessage(ChatRole.Tool, string.Empty);
                        toolMsg.Contents.Add(new FunctionResultContent(fc.Name, toolResult));
                        messages.Add(toolMsg);
                    }

                    // 循环继续，下一轮仍然使用流式获取，直到没有 ToolCalls
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "ChatTest4 调用失败，可能的 MCP 端点配置错误或网络不可达。");
                return Problem(title: "上游聊天服务异常", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChatTest4 未预期异常。");
                return Problem(title: "服务器内部错误", detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
