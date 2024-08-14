---
# These are optional elements. Feel free to remove any of them.
status: proposed
contact: markwallace
date: {YYYY-MM-DD when the decision was last updated}
deciders: sergeymenshykh, markwallace, rbarreto, dmytrostruk, westey
consulted: stephentoub, matthewbolanos, shrojans 
informed: 
---

# Text Search Service

## Context and Problem Statement

Semantic Kernel has support for searching using popular Vector databases e.g. Azure AI Search, Chroma, Milvus and also Web search engines e.g. Bing, Google.
There are two sets of abstractions and plugins depending on whether the developer wants to perform search against a Vector database or a Web search engine.
The current abstractions are experimental and the purpose of this ADR is to progress the design of the abstractions so that they can graduate to non experimental status.

There are two main use cases we need to support:

1. Enable Prompt Engineers to easily insert grounding information in prompts i.e. support for Retrieval-Augmented Generation scenarios.
2. Enable Developers to register search plugins which can be called by the LLM to retrieve additional data it needs to respond to a user ask i.e. support for Function Calling scenarios.

### Retrieval-Augmented Generation Scenarios

Retrieval-Augmented Generation (RAG) is a process of optimizing the output of an LLM, so it references authoritative data which may not be part of its training data when generating a response. This reduce the likelihood of hallucinations and also enables the provision of citations which the end user can use to independently verify the response from the LLM. RAG works by retrieving additional data that is relevant to the use query and then augment the prompt with this data before sending to the LLM.

Consider the following sample where the top Bing search results are included as additional data in the prompt.

```csharp
// Create a kernel with OpenAI chat completion
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAIChatCompletion(
        modelId: TestConfiguration.OpenAI.ChatModelId,
        apiKey: TestConfiguration.OpenAI.ApiKey,
        httpClient: httpClient);
Kernel kernel = kernelBuilder.Build();

// Create a text search using the Bing search service
var textSearch = new BingTextSearch(new(TestConfiguration.Bing.ApiKey));

// Build a text search plugin with Bing search service and add to the kernel
var searchPlugin = TextSearchKernelPluginFactory.CreateFromTextSearch<string>(textSearch, "SearchPlugin");
kernel.Plugins.Add(searchPlugin);

// Invoke prompt and use text search plugin to provide grounding information
var query = "What is the Semantic Kernel?";
KernelArguments arguments = new() { { "query", query } };
Console.WriteLine(await kernel.InvokePromptAsync("{{SearchPlugin.Search $query}}. {{$query}}", arguments));
```

This example works as follows:

1. Create a `BingTextSearch` which can perform Bing search queries.
2. Wrap the `BingTextSearch` as a plugin which can be called when rendering a prompt.
3. Insert a call to the plugin which performs a search using the user query.
4. The prompt will be augmented with the abstract from the top search results.

**Note:** In this case the abstract from the search result is the only data included in the prompt.
The LLM should use this data if it considers it relevant but there is no feedback mechanism to the user which would allow
them to verify the source of the data.

The following sample shows a solution to this problem.

```csharp
// Create a kernel with OpenAI chat completion
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAIChatCompletion(
        modelId: TestConfiguration.OpenAI.ChatModelId,
        apiKey: TestConfiguration.OpenAI.ApiKey,
        httpClient: httpClient);
Kernel kernel = kernelBuilder.Build();

// Create a text search using the Bing search service
var textSearch = new BingTextSearch(new(TestConfiguration.Bing.ApiKey));

// Build a text search plugin with Bing search service and add to the kernel
var searchPlugin = TextSearchKernelPluginFactory.CreateFromTextSearch<TextSearchResult>(textSearch, "SearchPlugin");
kernel.Plugins.Add(searchPlugin);

// Invoke prompt and use text search plugin to provide grounding information
var query = "What is the Semantic Kernel?";
string promptTemplate = @"
{{#with (SearchPlugin-GetSearchResults query)}}  
  {{#each this}}  
    Name: {{Name}}
    Value: {{Value}}
    Link: {{Link}}
    -----------------
  {{/each}}  
{{/with}}  

{{query}}

Include citations to the relevant information where it is referenced in the response.
";

KernelArguments arguments = new() { { "query", query } };
HandlebarsPromptTemplateFactory promptTemplateFactory = new();
Console.WriteLine(await kernel.InvokePromptAsync(
    promptTemplate,
    arguments,
    templateFormat: HandlebarsPromptTemplateFactory.HandlebarsTemplateFormat,
    promptTemplateFactory: promptTemplateFactory
));
```

This example works as follows:

1. Create a `BingTextSearch` which can perform Bing search queries and convert the response into a normalized format.
2. The normalized format is a Semantic Kernel abstraction called `TextSearchResult` which includes a name, value and link for each search result.
3. Wrap the `BingTextSearch` as a plugin which can be called when rendering a prompt.
4. Insert a call to the plugin which performs a search using the user query.
5. The prompt will be augmented with the name, value and link from the top search results.
6. The prompt also instructs the LLM to include citations to the relevant information in the response.

An example response would look like this:

```
The Semantic Kernel (SK) is a lightweight and powerful SDK developed by Microsoft that integrates Large Language Models (LLMs) such as OpenAI, Azure OpenAI, and Hugging Face with traditional programming languages like C#, Python, and Java ([GitHub](https://github.com/microsoft/semantic-kernel)). It facilitates the combination of natural language processing capabilities with pre-existing APIs and code, enabling developers to add large language capabilities to their applications swiftly ([What It Is and Why It Matters](https://techcommunity.microsoft.com/t5/microsoft-developer-community/semantic-kernel-what-it-is-and-why-it-matters/ba-p/3877022)).

The Semantic Kernel serves as a middleware that translates the AI model's requests into function calls, effectively bridging the gap between semantic functions (LLM tasks) and native functions (traditional computer code) ([InfoWorld](https://www.infoworld.com/article/2338321/semantic-kernel-a-bridge-between-large-language-models-and-your-code.html)). It also enables the automatic orchestration and execution of tasks using natural language prompting across multiple languages and platforms ([Hello, Semantic Kernel!](https://devblogs.microsoft.com/semantic-kernel/hello-world/)).

In addition to its core capabilities, Semantic Kernel supports advanced functionalities like prompt templating, chaining, and planning, which allow developers to create intricate workflows tailored to specific use cases ([Architecting AI Apps](https://devblogs.microsoft.com/semantic-kernel/architecting-ai-apps-with-semantic-kernel/)).

By describing your existing code to the AI models, Semantic Kernel effectively marshals the request to the appropriate function, returns results back to the LLM, and enables the AI agent to generate a final response ([Quickly Start](https://learn.microsoft.com/en-us/semantic-kernel/get-started/quick-start-guide)). This process brings unparalleled productivity and new experiences to application users ([Hello, Semantic Kernel!](https://devblogs.microsoft.com/semantic-kernel/hello-world/)).

The Semantic Kernel is an indispensable tool for developers aiming to build advanced AI applications by seamlessly integrating large language models with traditional programming frameworks ([Comprehensive Guide](https://gregdziedzic.com/understanding-semantic-kernel-a-comprehensive-guide/)).
```

**Note:** In this case there is a link to the relevant information so the end user can follow the links to verify the response.

### Function Calling Scenarios

Function calling allows you to connect LLMs to external tools and systems.
This capability can be used to enable an LLM to retrieve relevant information it needs in order to return a response to a user query.
In the context of this discussion we want to allow an LLM to perform a search to return relevant information.
We also want to enable developers to easily customize the search operations to improve the LLMs ability to retrieve the most relevant information.

We need to support the following use cases:

1. Enable developers to adapt an arbitrary text search implementation to be a search plugin which can be called by an LLM to perform searches.
   - Search results can be returned as text, or in a normalized format, or is a proprietary format associated with the text search implementation.
1. Enable developers to easily customize the search plugin, typical customizations will include:
   - Alter the search function metadata i.e. name, description, parameter details
   - Alter which search function(s) are included in the plugin
   - Alter the search function(s) behavior

Consider the following sample where the LLM can call Bing search to help it respond to the user ask.

```csharp
// Create a kernel with OpenAI chat completion
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAIChatCompletion(
        modelId: TestConfiguration.OpenAI.ChatModelId,
        apiKey: TestConfiguration.OpenAI.ApiKey,
        httpClient: httpClient);
Kernel kernel = kernelBuilder.Build();

// Create a search service with Bing search service
var textSearch = new BingTextSearch(new(TestConfiguration.Bing.ApiKey));

// Build a text search plugin with Bing search service and add to the kernel
var searchPlugin = TextSearchKernelPluginFactory.CreateFromTextSearch<string>(textSearch, "SearchPlugin");
kernel.Plugins.Add(searchPlugin);

// Invoke prompt and use text search plugin to provide grounding information
OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
KernelArguments arguments = new(settings);
Console.WriteLine(await kernel.InvokePromptAsync("What is the Semantic Kernel?", arguments));
```

This example works as follows:

1. Create a BingTextSearch which can perform Bing search queries.
1. Wrap the BingTextSearch as a plugin which can be advertised to the LLM.
1. Enable automatic function calling, which allows the LLM to call Bing search to retrieve relevant information.

**Note:** In this case the abstract from the search result is the only data included in the prompt. The LLM should use this data if it considers it relevant but there is no feedback mechanism to the user which would allow them to verify the source of the data.

The following sample shows a solution to this problem.

```csharp
// Create a kernel with OpenAI chat completion
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAIChatCompletion(
        modelId: TestConfiguration.OpenAI.ChatModelId,
        apiKey: TestConfiguration.OpenAI.ApiKey,
        httpClient: httpClient);
Kernel kernel = kernelBuilder.Build();

// Create a search service with Bing search service
var textSearch = new BingTextSearch(new(TestConfiguration.Bing.ApiKey));

// Build a text search plugin with Bing search service and add to the kernel
var searchPlugin = TextSearchKernelPluginFactory.CreateFromTextSearch<TextSearchResult>(textSearch, "SearchPlugin");
kernel.Plugins.Add(searchPlugin);

// Invoke prompt and use text search plugin to provide grounding information
OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
KernelArguments arguments = new(settings);
Console.WriteLine(await kernel.InvokePromptAsync("What is the Semantic Kernel? Include citations to the relevant information where it is referenced in the response.", arguments));
```

There are two changes in this sample:

1. 


## Decision Drivers

- An AI must be able to perform searches with a search plugin and get back “results” of type `T`.
- Application developers should be able to easily add a search plugin using a search connector with minimal lines of code (ideally one).
- Application developers must be able to provide connector specific settings.
- Application developers must be able to set required information e.g. `IndexName` for search providers.
- Application developers must be able to support custom schemas for search connectors. No fields should be required.
- Search service developers must be able to easily create a new search service that returns type `T`.
- Search service developers must be able to easily create a new search connector return type that inherits from `KernelSearchResults` (alternate suggestion `SearchResultContent`).
- The design must be flexible to support future requirements and different search modalities.

Need additional clarification

- Application developers must to be able to override the semantic descriptions of the search function(s) per instance registered via settings / inputs.
- Application developers must be able to optionally define the execution settings of an embedding service with a default being provided by the Kernel.
- Search service developers must be able to define the attributes of the search method (e.g., name, description, input names, input descriptions, return description).
- Application developers must be ab able to import a vector DB search connection using an ML index file.

### Future Requirements

- An AI can perform search with filters using a search plugin to get back “results” of type T. This will require a Connector Dev to implement a search interface that accepts a Filter object.
- Connector developers can decide which search filters are given to the AI by “default”.
- Application developers can override which filters the AI can use via search settings.
- Application developers can set the filters when they create the connection.

## Considered Options

- Define `ITextSearch` abstraction specifically for text search that uses generics
- Define `ITextSearch` abstraction specifically for text search that does not use generics
- {title of option 3}
- … <!-- numbers of options can vary -->

## Decision Outcome

Chosen option: "{title of option 1}", because
{justification. e.g., only option, which meets k.o. criterion decision driver | which resolves force {force} | … | comes out best (see below)}.

<!-- This is an optional element. Feel free to remove. -->

## Pros and Cons of the Options

### Define `ITextSearch` Abstraction with Generics

A new `ITextSearchService` abstraction is used to define the contract to perform a text based search.
`ITextSearchService` uses generics are each implementation is required to support returning search values as:

- `string` values, this will typically be the snippet or chunk associated with the search result.
- instances of `TextSearchResult`, this is a normalized result that has name, value and link properties.
- instances of the implementation specific result types e.g. Azure AI Search uses `SearchDocument` to represent search results.
- optionally instances of a specific type, although there may be limitations to this approach or or it may not be supported at all.

The class diagram below shows the class hierarchy.

<img src="./diagrams/text-search-service-abstraction.png" alt="ITextSearchService Abstraction" width="80%"/>

The abstraction contains the following interfaces and classes:

- `ITextSearchService` is the interface for text based search services. This can be invoked with a text query to return a collection of search results.
- `SearchExecutionSettings` provides execution settings for a search service. Some common settings e.g. `IndexName`, `Count`, `Offset` are defined.
- `KernelSearchResults` represents the search results returned from a `ISearchService` service. This provides access to the individual search results, underlying search result, metadata, ... This supports generics but an implementation can restrict the supported types. All implementations must support `string`, `TextSearchResult` and whatever native types the connector implementation supports. Some implementations will also support custom types.
- `TextSearchResult` represents a normalized text search result. All implementations must be able to return results using this type.

#### Return Results of Type `T`

All implementations of `ITextSearchService` **must** support returning the search results as a `string`. The `string` value is expected to contain the text value associated with the search result e.g. for Bing/Google this will be the snippet of text from the web page but for Azure AI Search this will be a designated field in the database.

Below is an example where Azure AI Search returns `string` search results. Note the `ValueField` setting controls which field value is returned.

```csharp
var searchService = new AzureAITextSearchService(
    endpoint: TestConfiguration.AzureAISearch.Endpoint,
    adminKey: TestConfiguration.AzureAISearch.ApiKey);

AzureAISearchExecutionSettings settings = new() { Index = IndexName, Count = 2, Offset = 2, ValueField = "chunk" };
KernelSearchResults<string> summaryResults = await searchService.SearchAsync<string>("What is the Semantic Kernel?", settings);
await foreach (string result in summaryResults.Results)
{
    Console.WriteLine(result);
}
```

Below is an example where Bing returns `string` search results. Note the `Snippet` value is returned in this case.

```csharp
var searchService = new BingTextSearchService(
    endpoint: TestConfiguration.Bing.Endpoint,
    apiKey: TestConfiguration.Bing.ApiKey);

KernelSearchResults<string> summaryResults = await searchService.SearchAsync<string>("What is the Semantic Kernel?", new() { Count = 2, Offset = 2 });
await foreach (string result in summaryResults.Results)
{
    Console.WriteLine(result);
}
```

All implementations of `ITextSearchService` **must** support returning the search results as a `TextSearchResult`. This is a common abstraction to present a search result that has the following properties:

- `Name` - The name of the search result e.g. this could be a web page title.
- `Value` - The text value associated with the search result e.g. this could be a web page snippet.
- `Link` - A link to the resource associated with the search result e.g. this could be the URL of a web page.
- `InnerContent` - The actual search result object to support breaking glass scenarios.

Below is an example where Azure AI Search returns `TextSearchResult` search results. Note the `NameField`, `ValueField` and `LinkField` settings control which field values are returned.

```csharp
AzureAISearchExecutionSettings settings = new() { Index = IndexName, Count = 2, Offset = 2, NameField = "title", ValueField = "chunk", LinkField = "metadata_spo_item_weburi" };
KernelSearchResults<TextSearchResult> textResults = await searchService.SearchAsync<TextSearchResult>("What is the Semantic Kernel?", settings);
await foreach (TextSearchResult result in textResults.Results)
{
    Console.WriteLine(result.Name);
    Console.WriteLine(result.Value);
    Console.WriteLine(result.Link);
}
```

Below is an example where Bing returns `TextSearchResult` search results. Note the `Name`, `Snippet` and `Url` values is returned in this case.

```csharp
var searchService = new BingTextSearchService(
    endpoint: TestConfiguration.Bing.Endpoint,
    apiKey: TestConfiguration.Bing.ApiKey);

KernelSearchResults<CustomSearchResult> searchResults = await searchService.SearchAsync<CustomSearchResult>("What is the Semantic Kernel?", new() { Count = 2 });
await foreach (CustomSearchResult result in searchResults.Results)
{
    Console.WriteLine(result.Name);
    Console.WriteLine(result.Snippet);
    Console.WriteLine(result.Url);
}
```

All implementations of `ITextSearchService` will support returning the implementation specific search results i.e. whatever the underlying client returns.

Below is an example where Azure AI Search returns `Azure.Search.Documents.Models.SearchDocument` search results.

```csharp
KernelSearchResults<SearchDocument> fullResults = await searchService.SearchAsync<SearchDocument>("What is the Semantic Kernel?", new() { Index = IndexName, Count = 2, Offset = 6 });
await foreach (SearchDocument result in fullResults.Results)
{
    Console.WriteLine(result.GetString("title"));
    Console.WriteLine(result.GetString("chunk_id"));
    Console.WriteLine(result.GetString("chunk"));
}
```

Below is an example where Bing returns `Microsoft.SemanticKernel.Plugins.Web.Bing.BingWebPage` search results.

```csharp
KernelSearchResults<BingWebPage> fullResults = await searchService.SearchAsync<BingWebPage>(query, new() { Count = 2, Offset = 6 });
await foreach (BingWebPage result in fullResults.Results)
{
    Console.WriteLine(result.Name);
    Console.WriteLine(result.Snippet);
    Console.WriteLine(result.Url);
    Console.WriteLine(result.DisplayUrl);
    Console.WriteLine(result.DateLastCrawled);
}
```

Implementations of `ITextSearchService` will optionally support returning the custom search results i.e. whatever the developer specifies.

Below is an example where Bing returns `Search.CustomSearchResult` search results.

```csharp
KernelSearchResults<CustomSearchResult> searchResults = await searchService.SearchAsync<CustomSearchResult>(query, new() { Count = 2 });
await foreach (CustomSearchResult result in searchResults.Results)
{
    WriteLine(result.Name);
    WriteLine(result.Snippet);
    WriteLine(result.Url);
}

public class CustomSearchResult
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("url")]
    public Uri? Url { get; set; }
    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }
}
```

#### Perform Search using Plugin

An out-of-the-box plugin is provided which allows a specific text search service implementation to be called.

Below is an example where two instances of the `TextSearchPlugin` are configured both using the Bing text search service.

1. Returns a single `string` search results. The result of calling the plugin with me the single `string` search result.
1. Returns two `BingWebPage` search results. The result of calling the plugin will be a JSON encoded string containing the two `TextSearchPlugin` search results.

```csharp
var searchService = new BingTextSearchService(
    endpoint: TestConfiguration.Bing.Endpoint,
    apiKey: TestConfiguration.Bing.ApiKey);

Kernel kernel = new();
var stringPlugin = new TextSearchPlugin<string>(searchService);
kernel.ImportPluginFromObject(stringPlugin, "StringSearch");
var pagePlugin = new TextSearchPlugin<BingWebPage>(searchService);
kernel.ImportPluginFromObject(pagePlugin, "PageSearch");

var function = kernel.Plugins["StringSearch"]["Search"];
var result = await kernel.InvokeAsync(function, new() { ["query"] = "What is the Semantic Kernel?" });
Console.WriteLine(result);

function = kernel.Plugins["PageSearch"]["Search"];
result = await kernel.InvokeAsync(function, new() { ["query"] = "What is the Semantic Kernel?", ["count"] = 2 });
Console.WriteLine(result);
```

Single `string` result

```
Semantic Kernel is an open-source SDK that lets you easily build agents that can call your existing code. As a highly extensible SDK, you can use Semantic Kernel with models from OpenAI, Azure OpenAI, Hugging Face, and more!
```

Two `TextSearchPlugin` search results

```json
[
    {
        "dateLastCrawled": "2024-05-01T06:08:00.0000000Z",
        "id": "https://api.bing.microsoft.com/api/v7/#WebPages.0",
        "language": "en",
        "isFamilyFriendly": true,
        "isNavigational": true,
        "name": "Create AI agents with Semantic Kernel | Microsoft Learn",
        "url": "https://learn.microsoft.com/en-us/semantic-kernel/overview/",
        "displayUrl": "https://learn.microsoft.com/en-us/semantic-kernel/overview",
        "snippet": "Semantic Kernel is an open-source SDK that lets you easily build agents that can call your existing code. As a highly extensible SDK, you can use Semantic Kernel with models from OpenAI, Azure OpenAI, Hugging Face, and more!"
    },
    {
        "dateLastCrawled": "2024-05-02T00:03:00.0000000Z",
        "id": "https://api.bing.microsoft.com/api/v7/#WebPages.1",
        "language": "en",
        "isFamilyFriendly": true,
        "isNavigational": false,
        "name": "Semantic Kernel: What It Is and Why It Matters",
        "url": "https://techcommunity.microsoft.com/t5/microsoft-developer-community/semantic-kernel-what-it-is-and-why-it-matters/ba-p/3877022",
        "displayUrl": "https://techcommunity.microsoft.com/t5/microsoft-developer-community/semantic-kernel...",
        "snippet": "Semantic Kernel is a new AI SDK, and a simple and yet powerful programming model that lets you add large language capabilities to your app in just a matter of minutes. It uses natural language prompting to create and execute semantic kernel AI tasks across multiple languages and platforms."
    }
]
```



#### Support ML Index File Format

TODO

Evaluation

- Good, because {argument a}
- Good, because {argument b}
<!-- use "neutral" if the given argument weights neither for good nor bad -->
- Neutral, because {argument c}
- Bad, because {argument d}
- … <!-- numbers of pros and cons can vary -->

### {title of other option}

{example | description | pointer to more information | …}

- Good, because {argument a}
- Good, because {argument b}
- Neutral, because {argument c}
- Bad, because {argument d}
- …

<!-- This is an optional element. Feel free to remove. -->

## More Information

{You might want to provide additional evidence/confidence for the decision outcome here and/or
document the team agreement on the decision and/or
define when this decision when and how the decision should be realized and if/when it should be re-visited and/or
how the decision is validated.
Links to other decisions and resources might appear here as well.}

### Current Design

The current design for search is divided into two implementations:

1. Search using a Memory Store i.e. Vector Database
1. Search using a Web Search Engine

In each case a plugin implementation is provided which allows the search to be integrated into prompts e.g. to provide additional context or to be called from a planner or using auto function calling with a LLM.

#### Memory Store Search

The diagram below shows the layers in the current design of the Memory Store search functionality.

<img src="./diagrams/text-search-service-imemorystore.png" alt="Current Memory Design" width="40%"/>

#### Web Search Engine Integration

The diagram below shows the layers in the current design of the Web Search Engine integration.

<img src="./diagrams/text-search-service-iwebsearchengineconnector.png" alt="Current Web Search Design" width="40%"/>

The Semantic Kernel currently includes experimental support for a `WebSearchEnginePlugin` which can be configured via a `IWebSearchEngineConnector` to integrate with a Web Search Services such as Bing or Google. The search results can be returned as a collection of string values or a collection of `WebPage` instances.

- The `string` values returned from the plugin represent a snippet of the search result in plain text.
- The `WebPage` instances returned from the plugin are a normalized subset of a complete search result. Each `WebPage` includes:
  - `name` The name of the search result web page
  - `url` The url of the search result web page
  - `snippet` A snippet of the search result in plain text

The current design doesn't support breaking glass scenario's or using custom types for the response values.

One goal of this ADR is to have a design where text search is unified into a single abstraction and a single plugin can be configured to perform web based searches or to search a vector store.
