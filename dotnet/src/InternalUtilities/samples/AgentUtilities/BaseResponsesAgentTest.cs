﻿// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.SemanticKernel.Agents.OpenAI;
using OpenAI;
using OpenAI.Responses;

/// <summary>
/// Base class for samples that demonstrate the usage of <see cref="OpenAIResponseAgent"/>.
/// </summary>
public abstract class BaseResponsesAgentTest : BaseAgentsTest<OpenAIResponseClient>
{
    protected BaseResponsesAgentTest(ITestOutputHelper output) : base(output)
    {
        var options = new OpenAIClientOptions();
        if (this.EnableLogging)
        {
            options.MessageLoggingPolicy = new MessageLoggingPolicy(new()
            {
                EnableLogging = true,
                EnableMessageLogging = true,
                EnableMessageContentLogging = true,
                LoggerFactory = this.LoggerFactory
            });
        }

        this.Client = new(model: "gpt-4o", credential: new ApiKeyCredential(TestConfiguration.OpenAI.ApiKey), options: options);
    }

    protected bool EnableLogging { get; set; } = true;

    /// <inheritdoc/>
    protected override OpenAIResponseClient Client { get; }
}
