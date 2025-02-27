﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GettingStarted;

/// <summary>
/// This example demonstrates how to declaratively create instances of <see cref="KernelAgent"/>.
/// </summary>
public class Step08_Declarative(ITestOutputHelper output) : BaseAgentsTest(output)
{
    [Fact]
    public async Task ChatCompletionAgentWithKernelAsync()
    {
        Kernel kernel = this.CreateKernelWithChatCompletion();

        var text =
            """
            type: chat_completion_agent
            name: StoryAgent
            description: Store Telling Agent
            instructions: Tell a story suitable for children about the topic provided by the user.
            """;
        var kernelAgentFactory = new ChatCompletionAgentFactory();

        var agent = await kernelAgentFactory.CreateAgentFromYamlAsync(text, kernel) as ChatCompletionAgent;

        await InvokeAgentAsync(agent!, "Cats and Dogs");
    }

    #region private
    /// <summary>
    /// Invoke the <see cref="ChatCompletionAgent"/> with the user input.
    /// </summary>
    private async Task InvokeAgentAsync(ChatCompletionAgent agent, string input)
    {
        ChatHistory chat = [];
        ChatMessageContent message = new(AuthorRole.User, input);
        chat.Add(message);
        this.WriteAgentChatMessage(message);

        await foreach (ChatMessageContent response in agent.InvokeAsync(chat))
        {
            chat.Add(response);

            this.WriteAgentChatMessage(response);
        }
    }
    #endregion
}
