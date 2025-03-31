﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents.Extensions;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Responses;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

/// <summary>
/// Represents a <see cref="KernelAgent"/> specialization based on Open AI Assistant / GPT.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class OpenAIResponseAgent : KernelAgent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIResponseAgent"/> class.
    /// </summary>
    /// <param name="client">The OpenAI provider for accessing the Responses API service.</param>
    public OpenAIResponseAgent(OpenAIResponseClient client)
    {
        Verify.NotNull(client);

        this.Client = client;
    }

    /// <summary>
    /// Expose client for additional use.
    /// </summary>
    public OpenAIResponseClient Client { get; }

    /// <summary>
    /// Storing of messages is enabled.
    /// </summary>
    public bool StoreEnabled { get; init; } = true;

    /// <inheritdoc/>
    public override async IAsyncEnumerable<AgentResponseItem<ChatMessageContent>> InvokeAsync(ICollection<ChatMessageContent> messages, AgentThread? thread = null, AgentInvokeOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Verify.NotNull(messages);

        var agentThread = await this.EnsureThreadExistsWithMessagesAsync(
            messages,
            thread,
            () => new OpenAIResponseAgentThread(this.Client, this.StoreEnabled),
            cancellationToken).ConfigureAwait(false);

        // Invoke responses with the updated chat history.
        var chatHistory = new ChatHistory();
        chatHistory.AddRange(messages);
        var invokeResults = this.InternalInvokeAsync(
            this.Name,
            chatHistory,
            agentThread,
            options,
            cancellationToken);

        // Notify the thread of new messages and return them to the caller.
        await foreach (var result in invokeResults.ConfigureAwait(false))
        {
            await this.NotifyThreadOfNewMessage(agentThread, result, cancellationToken).ConfigureAwait(false);
            yield return new(result, agentThread);
        }
    }

    /// <inheritdoc/>
    public override IAsyncEnumerable<AgentResponseItem<StreamingChatMessageContent>> InvokeStreamingAsync(ICollection<ChatMessageContent> messages, AgentThread? thread = null, AgentInvokeOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    [Experimental("SKEXP0110")]
    protected override Task<AgentChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    [Experimental("SKEXP0110")]
    protected override IEnumerable<string> GetChannelKeys()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    [Experimental("SKEXP0110")]
    protected override Task<AgentChannel> RestoreChannelAsync(string channelState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    #region private
    private async IAsyncEnumerable<ChatMessageContent> InternalInvokeAsync(
        string? agentName,
        ChatHistory history,
        OpenAIResponseAgentThread agentThread,
        AgentInvokeOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var kernel = options?.Kernel ?? this.Kernel;
        var arguments = this.MergeArguments(options?.KernelArguments);

        var overrideHistory = history;
        if (!this.StoreEnabled)
        {
            // Use the thread chat history
            overrideHistory = [.. agentThread.ChatHistory, .. history];
        }

        var inputItems = overrideHistory.Select(c => c.ToResponseItem());
        var creationOptions = new ResponseCreationOptions()
        {
            EndUserId = this.GetDisplayName(),
            Instructions = $"{this.Instructions}\n{options?.AdditionalInstructions}",
            StoredOutputEnabled = agentThread.StoreEnabled,
        };
        if (agentThread.StoreEnabled && agentThread.Id != null)
        {
            creationOptions.PreviousResponseId = agentThread.Id;
        }

        var clientResult = await this.Client.CreateResponseAsync(inputItems, creationOptions, cancellationToken).ConfigureAwait(false);
        var response = clientResult.Value;

        if (this.StoreEnabled)
        {
            // Update the response id
            agentThread.ResponseId = response.Id;
        }

        var messages = response.OutputItems.Select(o => o.ToChatMessageContent());

        foreach (ChatMessageContent message in messages)
        {
            message.AuthorName = this.Name;

            yield return message;
        }
    }
    #endregion
}
