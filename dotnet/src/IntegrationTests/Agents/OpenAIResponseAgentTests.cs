// Copyright (c) Microsoft. All rights reserved.
using System;
using System.ClientModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using OpenAI.Responses;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;

namespace SemanticKernel.IntegrationTests.Agents;
public sealed class OpenAIResponseAgentTests
{
    private readonly IConfigurationRoot _configuration = new ConfigurationBuilder()
        .AddJsonFile(path: "testsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddUserSecrets<OpenAIResponseAgentTests>()
        .Build();

    private const string SkipReason = null; //"OpenAI will often throttle requests. This test is for manual verification.";

    /// <summary>
    /// Integration test for <see cref="OpenAIResponseAgent"/> using function calling
    /// and targeting Open AI services.
    /// </summary>
    [Theory(Skip = SkipReason)]
    [InlineData("What is the special soup?", "Clam Chowder")]
    public async Task OpenAIResponseAgentTestAsync(string input, string expectedAnswerContains)
    {
        OpenAIConfiguration openAISettings = this._configuration.GetSection("OpenAI").Get<OpenAIConfiguration>()!;
        Assert.NotNull(openAISettings);

        await this.ExecuteAgentAsync(
            openAISettings.ChatModelId!,
            openAISettings.ApiKey!,
            input,
            expectedAnswerContains);
    }

    /// <summary>
    /// Integration test for <see cref="OpenAIResponseAgent"/> using function calling
    /// and targeting Open AI services.
    /// </summary>
    [Theory(Skip = SkipReason)]
    [InlineData("What is the special soup?", "Clam Chowder")]
    public async Task OpenAIResponseAgentStreamingAsync(string input, string expectedAnswerContains)
    {
        OpenAIConfiguration openAISettings = this._configuration.GetSection("OpenAI").Get<OpenAIConfiguration>()!;
        Assert.NotNull(openAISettings);

        await this.ExecuteStreamingAgentAsync(
            openAISettings.ChatModelId!,
            openAISettings.ApiKey!,
            input,
            expectedAnswerContains);
    }

    private async Task ExecuteAgentAsync(
        string modelId,
        string apiKey,
        string input,
        string expected)
    {
        // Arrange
        Kernel kernel = new();

        KernelPlugin plugin = KernelPluginFactory.CreateFromType<MenuPlugin>();
        var options = new OpenAIClientOptions();
        OpenAIResponseClient client = new(model: modelId, credential: new ApiKeyCredential(apiKey), options: options);
        OpenAIResponseAgent agent = new(client);

        AgentGroupChat chat = new();
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

        // Act
        StringBuilder builder = new();
        await foreach (var message in chat.InvokeAsync(agent))
        {
            builder.Append(message.Content);
        }

        // Assert
        Assert.Contains(expected, builder.ToString(), StringComparison.OrdinalIgnoreCase);
        await foreach (var message in chat.GetChatMessagesAsync())
        {
            AssertMessageValid(message);
        }
    }

    private async Task ExecuteStreamingAgentAsync(
        string modelId,
        string apiKey,
        string input,
        string expected)
    {
        // Arrange
        Kernel kernel = new();

        KernelPlugin plugin = KernelPluginFactory.CreateFromType<MenuPlugin>();
        var options = new OpenAIClientOptions();
        OpenAIResponseClient client = new(model: modelId, credential: new ApiKeyCredential(apiKey), options: options);
        OpenAIResponseAgent agent = new(client);

        AgentGroupChat chat = new();
        chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));

        // Act
        StringBuilder builder = new();
        await foreach (var message in chat.InvokeStreamingAsync(agent))
        {
            builder.Append(message.Content);
        }

        // Assert
        ChatMessageContent[] history = await chat.GetChatMessagesAsync().ToArrayAsync();
        Assert.Contains(expected, builder.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expected, history.First().Content, StringComparison.OrdinalIgnoreCase);
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
}
