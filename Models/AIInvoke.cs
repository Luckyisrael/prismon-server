namespace Prismon.Api.Models;

public class AIInvokeRequest
{
    public string UserId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty; // ID of the AI model to invoke
    public string InputType { get; set; } = string.Empty; // Text, Image, Json
    public string InputData { get; set; } = string.Empty; // Base64-encoded for images, JSON string, or raw text
    public string MCPTransport { get; set; } = "StreamableHTTP"; // Default for DeMCP
    public string ModelName { get; set; } = string.Empty; // Optional: e.g., gpt-3.5-turbo
}
public class AIInvokeResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty; // JSON string, text, or base64-encoded output
}

public class AIModelConfig
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // LocalML, ExternalAPI or MCP
    public string FilePath { get; set; } = string.Empty; // For LocalML: path to .zip
    public string ExternalApiUrl { get; set; } = string.Empty; // For ExternalAPI
    public string ExternalApiKey { get; set; } = string.Empty; // Optional API key
    public string InputType { get; set; } = string.Empty; // Text, Image, Json
    public string OutputType { get; set; } = string.Empty; // Text, Json
    public string ModelName { get; set; } = string.Empty; // Optional: e.g., gpt-3.5-turbo
}