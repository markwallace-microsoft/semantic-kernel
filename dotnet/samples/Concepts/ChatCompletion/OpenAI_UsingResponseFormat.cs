// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using Kusto.Cloud.Platform.Utils;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;

namespace ChatCompletion;

/// <summary>
/// This sample shows how to use the response format with an OpenAI Chat Completion.
/// The response format is an object specifying the format that the model must output.
/// </summary>
public class OpenAI_UsingResponseFormat(ITestOutputHelper output) : BaseTest(output)
{
    /// <summary>
    /// Show how to enable JSON mode, which ensures the message the model generates is valid JSON.
    /// </summary>
    [Fact]
    public async Task EnableJsonModeAsync()
    {
        // Create a logging handler to output HTTP requests and responses
        var handler = new LoggingHandler(new HttpClientHandler(), this.Output);
        HttpClient httpClient = new(handler);

        OpenAIChatCompletionService chatCompletionService = new(
            modelId: "gpt-4o-2024-08-06",
            apiKey: TestConfiguration.OpenAI.ApiKey,
            httpClient: httpClient);

        // Enabled structured output so model outputs now reliably adhere to developer-supplied JSON Schemas.
        var chatResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
            name: "animals",
            jsonSchema: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "animals": {
                            "type": "array",
                            "items": {
                                "type": "string",
                                "additionalProperties": false
                            }
                        }
                    },
                    "required": ["animals"],
                    "additionalProperties": false
                }
                """),
            strictSchemaEnabled: true);

        // Prompt execution settings will enable JSON mode and limit token count
        var settings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = chatResponseFormat,
            MaxTokens = 100,
        };

        // Important: when using JSON mode, you must also instruct the model to produce JSON yourself
        // via a system or user message. Without this, the model may generate an unending stream of whitespace
        // until the generation reaches the token limit, resulting in a long-running and seemingly "stuck" request.
        // Also note that the message content may be partially cut off if finish_reason="length", which indicates
        // the generation exceeded max_tokens or the conversation exceeded the max context length.
        var chatHistory = new ChatHistory("Respond in JSON format");

        // This request should complete within allowed token count
        chatHistory.AddUserMessage("List the ten most popular animals");
        OutputLastMessage(chatHistory);

        var replyMessage = await chatCompletionService.GetChatMessageContentAsync(chatHistory, settings);
        chatHistory.AddAssistantMessage(replyMessage.Content!);
        OutputLastMessage(chatHistory);
        OutputUsage(replyMessage);

        // This request should exceed the allowed token count
        chatHistory = new ChatHistory("Respond in JSON format");
        chatHistory.AddUserMessage("List the thirty most popular animals");
        OutputLastMessage(chatHistory);

        var messageContent = await GetChatMessageContentAsync(chatCompletionService, chatHistory, settings);
        chatHistory.AddAssistantMessage(messageContent);
        OutputLastMessage(chatHistory);
    }

    private async Task<string> GetChatMessageContentAsync(IChatCompletionService chatCompletionService, ChatHistory chatHistory, PromptExecutionSettings executionSettings)
    {
        StringBuilder messageContent = new();

        var replyMessage = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings);
        OutputUsage(replyMessage);
        var finishReason = replyMessage.Metadata?["FinishReason"]?.ToString() ?? string.Empty;
        messageContent.Append(replyMessage.Content);
        chatHistory.AddAssistantMessage(replyMessage.Content!);

        var loop = 0;
        while (finishReason.EqualsOrdinalIgnoreCase("length") && loop++ < 4)
        {
            // The model exceeded the token limit, so we need to continue the conversation
            chatHistory.AddUserMessage("Continue");
            replyMessage = await chatCompletionService.GetChatMessageContentAsync(chatHistory, executionSettings);
            OutputUsage(replyMessage);
            finishReason = replyMessage.Metadata?["FinishReason"]?.ToString() ?? string.Empty;
            messageContent.Append(replyMessage.Content);
            chatHistory.AddAssistantMessage(replyMessage.Content!);
        }

        return messageContent.ToString();
    }
}
