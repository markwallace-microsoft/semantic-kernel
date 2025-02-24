﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Agents;
using Xunit;

namespace SemanticKernel.Agents.UnitTests.Core.Extensions;

/// <summary>
/// Unit tests for <see cref="AgentDefinitionExtensions"/>.
/// </summary>
public class AgentDefinitionExtensionsTests
{
    /// <summary>
    /// Verify GetDefaultKernelArguments
    /// </summary>
    [Fact]
    public void VerifyGetDefaultKernelArguments()
    {
        // Arrange
        AgentDefinition agentDefinition = new()
        {
            Inputs =
            [
                new() { Name = "Input1", IsRequired = false, Default = "Default1" },
                new() { Name = "Input2", IsRequired = true, Default = "Default2" }
            ],
        };

        // Act
        var defaultArgs = agentDefinition.GetDefaultKernelArguments();

        // Assert
        Assert.NotNull(defaultArgs);
        Assert.Equal(2, defaultArgs.Count);
        Assert.Equal("Default1", defaultArgs["Input1"]);
        Assert.Equal("Default2", defaultArgs["Input2"]);
    }

    /// <summary>
    /// Verify GetFirstToolDefinition
    /// </summary>
    [Fact]
    public void VerifyGetFirstToolDefinition()
    {
        // Arrange
        AgentDefinition agentDefinition = new()
        {
            Tools =
            [
                new AgentToolDefinition { Type = "code_interpreter", Name = "Tool1" },
                new AgentToolDefinition { Type = "file_search", Name = "Tool2" },
            ],
        };

        // Act & Assert
        Assert.NotNull(agentDefinition.GetFirstToolDefinition("code_interpreter"));
        Assert.NotNull(agentDefinition.GetFirstToolDefinition("file_search"));
        Assert.Null(agentDefinition.GetFirstToolDefinition("openai"));
    }

    /// <summary>
    /// Verify HasToolType
    /// </summary>
    [Fact]
    public void VerifyIsEnableCodeInterpreter()
    {
        // Arrange
        AgentDefinition agentDefinition = new()
        {
            Tools =
            [
                new AgentToolDefinition { Type = "code_interpreter", Name = "Tool1" },
            ],
        };

        // Act & Assert
        Assert.True(agentDefinition.HasToolType("code_interpreter"));
    }

    /// <summary>
    /// Verify IsEnableFileSearch
    /// </summary>
    [Fact]
    public void VerifyIsEnableFileSearch()
    {
        // Arrange
        AgentDefinition agentDefinition = new()
        {
            Tools =
            [
                new AgentToolDefinition { Type = "file_search", Name = "Tool2" },
            ],
        };

        // Act & Assert
        Assert.True(agentDefinition.HasToolType("file_search"));
    }
}
