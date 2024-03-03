// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

public class Example87_DynamicKernelPlugins : BaseTest
{
    public Example87_DynamicKernelPlugins(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// Use dynamic kernel plugins to add helper functions to the kernel.
    /// </summary>
    [Fact]
    public async Task DynamicKernelPluginsAsync()
    {
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey);
        Kernel kernel = builder.Build();

        kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions("HelperFunctions", new[]
        {
            kernel.CreateFunctionFromMethod((Kernel kernel) => {
                if (!kernel.Plugins.TryGetFunction("HelperFunctions2", "GetLastName", out var getLastName))
                {
                    kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions("HelperFunctions2", new[] {
                        kernel.CreateFunctionFromMethod(() => "Smith", "GetLastName", "Gets the last name of the user"),
                    }));
                }

                return "John";
            }, "GetFirstName", "Gets the first name of the user"),
        }));

        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        chatHistory.AddUserMessage("What is my first and last name? Use functions available to you.");
        StringBuilder sb = new();
        await foreach (var update in chat.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel))
        {
            if (update.Content is not null)
            {
                sb.Append(update.Content);
            }
        }
        chatHistory.AddAssistantMessage(sb.ToString());
        WriteLine(sb.ToString());
    }
}
