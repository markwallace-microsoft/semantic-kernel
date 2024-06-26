﻿// Copyright (c) Microsoft. All rights reserved.

/* 
Phase 01 : This class was created adapting and merging ClientCore and OpenAIClientCore classes.
System.ClientModel changes were added and adapted to the code as this package is now used as a dependency over OpenAI package.
All logic from original ClientCore and OpenAIClientCore were preserved.

Phase 02 :
- Moved AddAttributes usage to the constructor, avoiding the need verify and adding it in the services.
- Added ModelId attribute to the OpenAIClient constructor.
- Added WhiteSpace instead of empty string for ApiKey to avoid exception from OpenAI Client on custom endpoints added an issue in OpenAI SDK repo. https://github.com/openai/openai-dotnet/issues/90
*/

using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.Services;
using OpenAI;

#pragma warning disable CA2208 // Instantiate argument exceptions correctly

namespace Microsoft.SemanticKernel.Connectors.OpenAI;

/// <summary>
/// Base class for AI clients that provides common functionality for interacting with OpenAI services.
/// </summary>
internal partial class ClientCore
{
    /// <summary>
    /// White space constant.
    /// </summary>
    private const string SingleSpace = " ";

    /// <summary>
    /// Gets the attribute name used to store the organization in the <see cref="IAIService.Attributes"/> dictionary.
    /// </summary>
    internal const string OrganizationKey = "Organization";

    /// <summary>
    /// Default OpenAI API endpoint.
    /// </summary>
    private const string OpenAIV1Endpoint = "https://api.openai.com/v1";

    /// <summary>
    /// Identifier of the default model to use
    /// </summary>
    internal string ModelId { get; init; } = string.Empty;

    /// <summary>
    /// Non-default endpoint for OpenAI API.
    /// </summary>
    internal Uri? Endpoint { get; init; }

    /// <summary>
    /// Logger instance
    /// </summary>
    internal ILogger Logger { get; init; }

    /// <summary>
    /// OpenAI / Azure OpenAI Client
    /// </summary>
    internal OpenAIClient Client { get; }

    /// <summary>
    /// Storage for AI service attributes.
    /// </summary>
    internal Dictionary<string, object?> Attributes { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientCore"/> class.
    /// </summary>
    /// <param name="modelId">Model name.</param>
    /// <param name="apiKey">OpenAI API Key.</param>
    /// <param name="organizationId">OpenAI Organization Id (usually optional).</param>
    /// <param name="endpoint">OpenAI compatible API endpoint.</param>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    internal ClientCore(
        string modelId,
        string? apiKey = null,
        string? organizationId = null,
        Uri? endpoint = null,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        Verify.NotNullOrWhiteSpace(modelId);

        this.Logger = logger ?? NullLogger.Instance;
        this.ModelId = modelId;

        this.AddAttribute(AIServiceExtensions.ModelIdKey, modelId);

        // Accepts the endpoint if provided, otherwise uses the default OpenAI endpoint.
        this.Endpoint = endpoint ?? httpClient?.BaseAddress;
        if (this.Endpoint is null)
        {
            Verify.NotNullOrWhiteSpace(apiKey); // For Public OpenAI Endpoint a key must be provided.
            this.Endpoint = new Uri(OpenAIV1Endpoint);
        }
        else if (string.IsNullOrEmpty(apiKey))
        {
            // Avoids an exception from OpenAI Client when a custom endpoint is provided without an API key.
            apiKey = SingleSpace;
        }

        this.AddAttribute(AIServiceExtensions.EndpointKey, this.Endpoint.ToString());

        var options = GetOpenAIClientOptions(httpClient, this.Endpoint);
        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            options.AddPolicy(new AddHeaderRequestPolicy("OpenAI-Organization", organizationId!), PipelinePosition.PerCall);

            this.AddAttribute(ClientCore.OrganizationKey, organizationId);
        }

        this.Client = new OpenAIClient(apiKey!, options);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientCore"/> class using the specified OpenAIClient.
    /// Note: instances created this way might not have the default diagnostics settings,
    /// it's up to the caller to configure the client.
    /// </summary>
    /// <param name="modelId">Azure OpenAI model ID or deployment name, see https://learn.microsoft.com/azure/cognitive-services/openai/how-to/create-resource</param>
    /// <param name="openAIClient">Custom <see cref="OpenAIClient"/>.</param>
    /// <param name="logger">The <see cref="ILoggerFactory"/> to use for logging. If null, no logging will be performed.</param>
    internal ClientCore(
        string modelId,
        OpenAIClient openAIClient,
        ILogger? logger = null)
    {
        Verify.NotNullOrWhiteSpace(modelId);
        Verify.NotNull(openAIClient);

        this.Logger = logger ?? NullLogger.Instance;
        this.ModelId = modelId;
        this.Client = openAIClient;

        this.AddAttribute(AIServiceExtensions.ModelIdKey, modelId);
    }

    /// <summary>
    /// Logs OpenAI action details.
    /// </summary>
    /// <param name="callerMemberName">Caller member name. Populated automatically by runtime.</param>
    internal void LogActionDetails([CallerMemberName] string? callerMemberName = default)
    {
        if (this.Logger.IsEnabled(LogLevel.Information))
        {
            this.Logger.LogInformation("Action: {Action}. OpenAI Model ID: {ModelId}.", callerMemberName, this.ModelId);
        }
    }

    /// <summary>
    /// Allows adding attributes to the client.
    /// </summary>
    /// <param name="key">Attribute key.</param>
    /// <param name="value">Attribute value.</param>
    internal void AddAttribute(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            this.Attributes.Add(key, value);
        }
    }

    /// <summary>Gets options to use for an OpenAIClient</summary>
    /// <param name="httpClient">Custom <see cref="HttpClient"/> for HTTP requests.</param>
    /// <param name="endpoint">Endpoint for the OpenAI API.</param>
    /// <returns>An instance of <see cref="OpenAIClientOptions"/>.</returns>
    private static OpenAIClientOptions GetOpenAIClientOptions(HttpClient? httpClient, Uri? endpoint)
    {
        OpenAIClientOptions options = new()
        {
            ApplicationId = HttpHeaderConstant.Values.UserAgent,
            Endpoint = endpoint
        };

        options.AddPolicy(new AddHeaderRequestPolicy(HttpHeaderConstant.Names.SemanticKernelVersion, HttpHeaderConstant.Values.GetAssemblyVersion(typeof(ClientCore))), PipelinePosition.PerCall);

        if (httpClient is not null)
        {
            options.Transport = new HttpClientPipelineTransport(httpClient);
            options.RetryPolicy = new ClientRetryPolicy(maxRetries: 0); // Disable retry policy if and only if a custom HttpClient is provided.
            options.NetworkTimeout = Timeout.InfiniteTimeSpan; // Disable default timeout
        }

        return options;
    }

    /// <summary>
    /// Invokes the specified request and handles exceptions.
    /// </summary>
    /// <typeparam name="T">Type of the response.</typeparam>
    /// <param name="request">Request to invoke.</param>
    /// <returns>Returns the response.</returns>
    private static async Task<T> RunRequestAsync<T>(Func<Task<T>> request)
    {
        try
        {
            return await request.Invoke().ConfigureAwait(false);
        }
        catch (ClientResultException e)
        {
            throw e.ToHttpOperationException();
        }
    }
}