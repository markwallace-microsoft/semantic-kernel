﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Responses;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

[ExcludeFromCodeCoverage]
internal static class ResponseItemExtensions
{
    /// <summary>
    /// Converts a <see cref="ResponseItem"/> instance to a <see cref="ChatMessageContent"/>.
    /// </summary>
    /// <param name="item">The response item to convert.</param>
    /// <returns>A <see cref="ChatMessageContent"/> instance.</returns>
    public static ChatMessageContent ToChatMessageContent(this ResponseItem item)
    {
        if (item is MessageResponseItem messageResponseItem)
        {
            var role = messageResponseItem.Role.ToAuthorRole();
            var collection = messageResponseItem.Content.ToChatMessageContentItemCollection();

            return new ChatMessageContent(role, collection, innerContent: messageResponseItem);
        }
        throw new InvalidOperationException();
    }

    #region private
    private static ChatMessageContentItemCollection ToChatMessageContentItemCollection(this IList<ResponseContentPart> content)
    {
        var collection = new ChatMessageContentItemCollection();
        foreach (var part in content)
        {
            if (part.Kind == ResponseContentPartKind.OutputText)
            {
                collection.Add(new TextContent(part.Text));
            }
        }
        return collection;
    }

    private static AuthorRole ToAuthorRole(this MessageRole messageRole)
    {
        return messageRole switch
        {
            MessageRole.Assistant => AuthorRole.Assistant,
            MessageRole.Developer => AuthorRole.Developer,
            MessageRole.System => AuthorRole.System,
            MessageRole.User => AuthorRole.User,
            _ => new AuthorRole("unknown"),
        };
    }
    #endregion
}
