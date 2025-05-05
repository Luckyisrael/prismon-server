namespace Prismon.Api.Models;

public class AIModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Guid AppId { get; set; }
    public App App { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // LocalML, ExternalAPI
    public string FilePath { get; set; } = string.Empty; // Path to .zip for LocalML
    public string ExternalApiUrl { get; set; } = string.Empty; // For ExternalAPI or MCP server
    public string ExternalApiKey { get; set; } = string.Empty; // Encrypted or MCP server
    public string InputType { get; set; } = string.Empty; // Text, Image, Json
    public string OutputType { get; set; } = string.Empty; // Text, Json
    public string ModelName { get; set; } = string.Empty; //Optional: e.g., gpt-3.5-turbo
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
}

public class RegisterModelResponse
{
    public bool Succeeded { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}


public class AIInvocation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public AIModel Model { get; set; } = null!;
    public string InputType { get; set; } = string.Empty;
    public string InputData { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime InvokedAt { get; set; } = DateTime.UtcNow;
}

