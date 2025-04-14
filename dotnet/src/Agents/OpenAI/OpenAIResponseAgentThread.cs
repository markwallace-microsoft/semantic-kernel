﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Responses;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

/// <summary>
/// Represents a conversation thread for an OpenAI Response API based agent when store is enabled.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class OpenAIResponseAgentThread : AgentThread
{
    private readonly OpenAIResponseClient _client;
    private bool _isDeleted = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIResponseAgentThread"/> class.
    /// </summary>
    /// <param name="client">The agents client to use for interacting with responses.</param>
    public OpenAIResponseAgentThread(OpenAIResponseClient client)
    {
        Verify.NotNull(client);

        this._client = client;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIResponseAgentThread"/> class that resumes an existing response.
    /// </summary>
    /// <param name="client">The agents client to use for interacting with responses.</param>
    /// <param name="responseId">The ID of an existing response to resume.</param>
    public OpenAIResponseAgentThread(OpenAIResponseClient client, string responseId)
    {
        Verify.NotNull(client);
        Verify.NotNull(responseId);

        this._client = client;
        this.ResponseId = responseId;
    }

    /// <summary>
    /// The current response id.
    /// </summary>
    internal string? ResponseId { get; set; }

    /// <inheritdoc />
    public override string? Id => this.ResponseId;

    /// <inheritdoc />
    protected override Task<string?> CreateInternalAsync(CancellationToken cancellationToken = default)
    {
        if (this._isDeleted)
        {
            throw new InvalidOperationException("This thread has been deleted and cannot be recreated.");
        }

        // Id will not be available until after a message is sent
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc />
    protected override Task DeleteInternalAsync(CancellationToken cancellationToken = default)
    {
        if (this._isDeleted)
        {
            return Task.CompletedTask;
        }

        if (this.ResponseId is null)
        {
            throw new InvalidOperationException("This thread cannot be deleted, since it has not been created.");
        }

        this._isDeleted = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override Task OnNewMessageInternalAsync(ChatMessageContent newMessage, CancellationToken cancellationToken = default)
    {
        if (this._isDeleted)
        {
            throw new InvalidOperationException("This thread has been deleted and cannot be used anymore.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatMessageContent> GetMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (this._isDeleted)
        {
            throw new InvalidOperationException("This thread has been deleted and cannot be used anymore.");
        }

        if (!string.IsNullOrEmpty(this.ResponseId))
        {
            var options = new ResponseItemCollectionOptions();
            var collectionResult = this._client.GetResponseInputItemsAsync(this.ResponseId, options, cancellationToken).ConfigureAwait(false);
            await foreach (var responseItem in collectionResult)
            {
                yield return responseItem.ToChatMessageContent();
            }
        }

        yield break;
    }
}
