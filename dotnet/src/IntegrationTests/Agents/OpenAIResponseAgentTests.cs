﻿// Copyright (c) Microsoft. All rights reserved.
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using OpenAI.Responses;
using SemanticKernel.IntegrationTests.TestSettings;
using xRetry;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Agents;

#pragma warning disable xUnit1004 // Contains test methods used in manual verification. Disable warning for this file only.

public sealed class OpenAIResponseAgentTests(ITestOutputHelper output)
{
    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<OpenAIResponseAgentTests>()
            .Build();

    /// <summary>
    /// Integration test for <see cref="OpenAIResponseAgent"/>.
    /// </summary>
    [RetryTheory(typeof(HttpOperationException))]
    [InlineData("What is the capital of France?", "Paris", true)]
    [InlineData("What is the capital of France?", "Paris", false)]
    public async Task OpenAIResponseAgentInvokeAsync(string input, string expectedAnswerContains, bool isOpenAI)
    {
        OpenAIResponseClient client = this.CreateClient(isOpenAI);

        await this.ExecuteAgentAsync(
            client,
            input,
            expectedAnswerContains);
    }

    /// <summary>
    /// Integration test for <see cref="OpenAIResponseAgent"/> using a thread.
    /// </summary>
    [RetryTheory(typeof(HttpOperationException))]
    [InlineData("What is the capital of France?", "Paris", true)]
    [InlineData("What is the capital of France?", "Paris", false)]
    public async Task OpenAIResponseAgentInvokeWithThreadAsync(string input, string expectedAnswerContains, bool isOpenAI)
    {
        // Arrange
        OpenAIResponseClient client = this.CreateClient(isOpenAI);
        Kernel kernel = new();
        OpenAIResponseAgent agent = new(client)
        {
            Instructions = "Answer all queries in English and French."
        };

        // Act & Assert
        AgentThread? thread = null;
        try
        {
            StringBuilder builder = new();
            await foreach (var responseItem in agent.InvokeAsync(input))
            {
                Assert.NotNull(responseItem);
                Assert.NotNull(responseItem.Message);
                Assert.NotNull(responseItem.Thread);
                Assert.Equal(AuthorRole.Assistant, responseItem.Message.Role);

                builder.Append(responseItem.Message.Content);
                thread = responseItem.Thread;
            }
        }
        finally
        {
            Assert.NotNull(thread);
            await thread.DeleteAsync();

            // Copy of the thread that doesn't have the deleted state
            thread = new OpenAIResponseAgentThread(client, thread.Id!);
            await Assert.ThrowsAsync<AgentThreadOperationException>(async () => await thread.DeleteAsync());
        }
    }

    /// <summary>
    /// Integration test for <see cref="OpenAIResponseAgent"/> using streaming.
    /// </summary>
    [RetryTheory(typeof(HttpOperationException))]
    [InlineData("What is the capital of France?", "Paris", true)]
    [InlineData("What is the capital of France?", "Paris", false)]
    public async Task OpenAIResponseAgentInvokeStreamingAsync(string input, string expectedAnswerContains, bool isOpenAI)
    {
        OpenAIResponseClient client = this.CreateClient(isOpenAI);

        await this.ExecuteStreamingAgentAsync(
            client,
            input,
            expectedAnswerContains);
    }

    /// <summary>
    /// Integration test for <see cref="OpenAIResponseAgent"/> adding override instructions to a thread on invocation via custom options.
    /// </summary>
    [RetryTheory(typeof(HttpOperationException))]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OpenAIResponseAgentInvokeStreamingWithThreadAsync(bool isOpenAI)
    {
        // Arrange
        OpenAIResponseClient client = this.CreateClient(isOpenAI);
        OpenAIResponseAgent agent = new(client)
        {
            Instructions = "Answer all queries in English and French."
        };

        OpenAIResponseAgentThread agentThread = new(client);

        // Act
        string? responseText = null;
        try
        {
            var message = new ChatMessageContent(AuthorRole.User, "What is the capital of France?");
            var responseMessages = await agent.InvokeStreamingAsync(
                message,
                agentThread,
                new OpenAIResponseAgentInvokeOptions()
                {
                    AdditionalInstructions = "Respond to all user questions with 'Computer says no'.",
                }).ToArrayAsync();

            responseText = string.Join(string.Empty, responseMessages.Select(ri => ri.Message.Content));
        }
        finally
        {
            await agentThread.DeleteAsync();
        }

        // Assert
        Assert.NotNull(responseText);
        Assert.Contains("Computer says no", responseText);
    }

    #region private
    /// <summary>
    /// Enable or disable logging for the tests.
    /// </summary>
    private bool EnableLogging { get; set; } = false;

    private async Task ExecuteAgentAsync(
        OpenAIResponseClient client,
        string input,
        string expected)
    {
        // Arrange
        Kernel kernel = new();
        OpenAIResponseAgent agent = new(client)
        {
            Instructions = "Answer all queries in English and French."
        };

        // Act & Assert
        StringBuilder builder = new();
        AgentThread? thread = null;
        await foreach (var responseItem in agent.InvokeAsync(input))
        {
            Assert.NotNull(responseItem);
            Assert.NotNull(responseItem.Message);
            Assert.NotNull(responseItem.Thread);
            Assert.Equal(AuthorRole.Assistant, responseItem.Message.Role);

            builder.Append(responseItem.Message.Content);
            thread = responseItem.Thread;
        }

        // Assert
        Assert.NotNull(thread);
        Assert.Contains(expected, builder.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExecuteStreamingAgentAsync(
        OpenAIResponseClient client,
        string input,
        string expected)
    {
        // Arrange
        Kernel kernel = new();
        OpenAIResponseAgent agent = new(client)
        {
            Instructions = "Answer all queries in English and French."
        };

        // Act
        StringBuilder builder = new();
        AgentThread? thread = null;
        await foreach (var responseItem in agent.InvokeStreamingAsync(input))
        {
            builder.Append(responseItem.Message.Content);
            thread = responseItem.Thread;
        }

        // Assert
        Assert.NotNull(thread);
        Assert.Contains(expected, builder.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertMessageValid(ChatMessageContent message)
    {
        if (message.Items.OfType<FunctionResultContent>().Any())
        {
            Assert.Equal(AuthorRole.Tool, message.Role);
            return;
        }

        if (message.Items.OfType<FunctionCallContent>().Any())
        {
            Assert.Equal(AuthorRole.Assistant, message.Role);
            return;
        }

        Assert.Equal(string.IsNullOrEmpty(message.AuthorName) ? AuthorRole.User : AuthorRole.Assistant, message.Role);
    }

    private OpenAIConfiguration ReadConfiguration()
    {
        OpenAIConfiguration? configuration = this._configuration.GetSection("OpenAI").Get<OpenAIConfiguration>();
        Assert.NotNull(configuration);
        return configuration;
    }

    private OpenAIResponseClient CreateClient(bool isOpenAI)
    {
        OpenAIResponseClient client;
        if (isOpenAI)
        {
            client = this.CreateClient(this._configuration.GetSection("OpenAI").Get<OpenAIConfiguration>());
        }
        else
        {
            client = this.CreateClient(this._configuration.GetSection("AzureOpenAI").Get<AzureOpenAIConfiguration>());
        }

        return client;
    }

    private OpenAIResponseClient CreateClient(OpenAIConfiguration? configuration)
    {
        Assert.NotNull(configuration);

        OpenAIClientOptions options = new();

        if (this.EnableLogging)
        {
            options.ClientLoggingOptions = new ClientLoggingOptions
            {
                EnableLogging = true,
                EnableMessageLogging = true,
                EnableMessageContentLogging = true,
                LoggerFactory = new RedirectOutput(output),
            };
        }

        return new OpenAIResponseClient(configuration.ChatModelId, new ApiKeyCredential(configuration.ApiKey), options);
    }

    private OpenAIResponseClient CreateClient(AzureOpenAIConfiguration? configuration)
    {
        Assert.NotNull(configuration);

        AzureOpenAIClientOptions options = new();

        if (this.EnableLogging)
        {
            options.ClientLoggingOptions = new ClientLoggingOptions
            {
                EnableLogging = true,
                EnableMessageLogging = true,
                EnableMessageContentLogging = true,
                LoggerFactory = new RedirectOutput(output),
            };
        }

        var azureClient = new AzureOpenAIClient(new Uri(configuration.Endpoint), new AzureCliCredential(), options);
        return azureClient.GetOpenAIResponseClient(configuration.ChatDeploymentName);
    }

    public sealed class MenuPlugin
    {
        [KernelFunction, Description("Provides a list of specials from the menu.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Too smart")]
        public string GetSpecials()
        {
            return @"
Special Soup: Clam Chowder
Special Salad: Cobb Salad
Special Drink: Chai Tea
";
        }

        [KernelFunction, Description("Provides the price of the requested menu item.")]
        public string GetItemPrice(
            [Description("The name of the menu item.")]
            string menuItem)
        {
            return "$9.99";
        }
    }
    #endregion
}
