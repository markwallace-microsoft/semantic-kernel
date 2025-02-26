﻿// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.SemanticKernel.Agents;

/// <summary>
/// Helper methods for creating <see cref="AgentDefinition"/> from YAML.
/// </summary>
[Experimental("SKEXP0110")]
public static class AgentDefinitionYaml
{
    /// <summary>
    /// Convert the given YAML text to a <see cref="AgentDefinition"/> model.
    /// </summary>
    /// <param name="text">YAML representation of the <see cref="AgentDefinition"/> to use to create the prompt function.</param>
    public static AgentDefinition FromYaml(string text)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeConverter(new PromptExecutionSettingsTypeConverter())
            .Build();

        return deserializer.Deserialize<AgentDefinition>(text);
    }
}
