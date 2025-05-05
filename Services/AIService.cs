using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using Prismon.Api.Data;
using Prismon.Api.Interface;
using Prismon.Api.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Prismon.Api.Services;

public class AIService : IAIService
{
    private readonly PrismonDbContext _dbContext;
    private readonly ILogger<AIService> _logger;
    private readonly MLContext _mlContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly string _modelStoragePath = Path.Combine(Directory.GetCurrentDirectory(), "ai_models");
    private readonly IDataProtector _protector;

    public AIService(PrismonDbContext dbContext,
        ILogger<AIService> logger,
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _logger = logger;
        _mlContext = new MLContext(seed: 0);
        _httpClientFactory = httpClientFactory;
        _dataProtectionProvider = dataProtectionProvider;
        _protector = _dataProtectionProvider.CreateProtector("Prismon.AIModel.ExternalApiKey");
        Directory.CreateDirectory(_modelStoragePath);
    }


    public async Task<RegisterModelResponse> RegisterModelAsync(string appId, AIModelConfig config)
    {
        try
        {
            var app = await _dbContext.Apps.FirstOrDefaultAsync(a => a.Id == Guid.Parse(appId));
            if (app == null)
            {
                _logger.LogWarning("Invalid AppId {AppId} for model registration", appId);
                return new RegisterModelResponse
                {
                    Succeeded = false,
                    ModelId = null,
                    Message = "Invalid AppId"
                };
            }
    
            if (!ValidateModelConfig(config))
            {
                _logger.LogWarning("Invalid model configuration for AppId {AppId}", appId);
                return new RegisterModelResponse
                {
                    Succeeded = false,
                    ModelId = null,
                    Message = "Invalid model configuration"
                };
            }
    
            var model = new AIModel
            {
                AppId = Guid.Parse(appId),
                Name = config.Name,
                Type = config.Type,
                InputType = config.InputType,
                OutputType = config.OutputType,
                ExternalApiUrl = config.ExternalApiUrl,
                ExternalApiKey = string.IsNullOrEmpty(config.ExternalApiKey)
                    ? string.Empty
                    : _protector.Protect(config.ExternalApiKey) // Encrypt API key
            };
    
            if (config.Type == "LocalML")
            {
                var filePath = Path.Combine(_modelStoragePath, $"{Guid.NewGuid()}.zip");
                await File.WriteAllBytesAsync(filePath, Convert.FromBase64String(config.FilePath));
                model.FilePath = filePath;
            }
    
            _dbContext.AIModels.Add(model);
            await _dbContext.SaveChangesAsync();
    
            _logger.LogInformation("Registered AI model {ModelId} for AppId {AppId}", model.Id, appId);
            return new RegisterModelResponse
            {
                Succeeded = true,
                ModelId = model.Id,
                Message = "Model registered successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering AI model for AppId {AppId}", appId);
            return new RegisterModelResponse
            {
                Succeeded = false,
                ModelId = null,
                Message = $"Error registering AI model: {ex.Message}"
            };
        }
    }


    public async Task<AIInvokeResponse> InvokeAIAsync(AIInvokeRequest request)
    {
        try
        {
            // Validate request
            if (!ValidateInput(request))
            {
                _logger.LogWarning("Invalid AI invoke request for UserId {UserId}", request.UserId);
                return new AIInvokeResponse { Succeeded = false, Message = "Invalid input" };
            }

            // Authenticate
            var app = await _dbContext.Apps.FirstOrDefaultAsync(a => a.ApiKey == request.ApiKey);
            if (app == null)
            {
                _logger.LogWarning("Invalid API key: {ApiKey}", request.ApiKey);
                return new AIInvokeResponse { Succeeded = false, Message = "Invalid API key" };
            }
            _logger.LogDebug("Looking up DAppUser for UserId {UserId}, AppId {AppId}", request.UserId, app.Id);
            Guid userGuid;
            if (!Guid.TryParse(request.UserId, out userGuid))
            {
                _logger.LogError("Invalid UserId format: {UserId}", request.UserId);
            }

            var dappUser = await _dbContext.DAppUsers.FirstOrDefaultAsync(u => u.Id == userGuid && u.AppId == app.Id);

            if (dappUser == null)
            {
                _logger.LogWarning("No DAppUser found for UserId {UserId}, AppId {AppId}", request.UserId, app.Id);
                return new AIInvokeResponse { Succeeded = false, Message = "User not found" };
            }

            // Get model
            var model = await _dbContext.AIModels.FirstOrDefaultAsync(m => m.Id == request.ModelId && m.AppId == app.Id);
            if (model == null)
            {
                _logger.LogWarning("Model {ModelId} not found for AppId {AppId}", request.ModelId, app.Id);
                return new AIInvokeResponse { Succeeded = false, Message = "Model not found" };
            }

            if (model.InputType != request.InputType)
            {
                _logger.LogWarning("Input type mismatch for Model {ModelId}: expected {Expected}, got {Actual}",
                    request.ModelId, model.InputType, request.InputType);
                return new AIInvokeResponse { Succeeded = false, Message = "Input type mismatch" };
            }

            // Invoke model
            string output;
            if (model.Type == "LocalML")
            {
                output = await InvokeLocalMLModel(model, request.InputData);
            }
            else if (model.Type == "ExternalAPI")
            {
                output = await InvokeExternalAPI(model, request.InputData);
            }
            else if (model.Type == "MCP")
            {
                output = await InvokeMCPModel(model, request.InputData, request.MCPTransport, request.ModelName);
            }
            else
            {
                _logger.LogWarning("Unsupported model type {Type} for Model {ModelId}", model.Type, model.Id);
                return new AIInvokeResponse { Succeeded = false, Message = "Unsupported model type" };
            }

            // Store invocation
            var invocation = new AIInvocation
            {
                UserId = request.UserId,
                ModelId = model.Id,
                InputType = request.InputType,
                InputData = request.InputData,
                Output = output,
                Succeeded = true
            };
            _dbContext.AIInvocations.Add(invocation);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("AI invocation successful for UserId {UserId}, ModelId {ModelId}", request.UserId, model.Id);
            return new AIInvokeResponse
            {
                Succeeded = true,
                Message = "AI invocation successful",
                Output = output
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking AI for UserId {UserId}, ModelId {ModelId}", request.UserId, request.ModelId);
            var invocation = new AIInvocation
            {
                UserId = request.UserId,
                ModelId = request.ModelId,
                InputType = request.InputType,
                InputData = request.InputData,
                Succeeded = false,
                ErrorMessage = ex.Message
            };
            _dbContext.AIInvocations.Add(invocation);
            await _dbContext.SaveChangesAsync();

            return new AIInvokeResponse
            {
                Succeeded = false,
                Message = $"AI invocation failed: {ex.Message}"
            };
        }
    }

    private bool ValidateInput(AIInvokeRequest request)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.ApiKey) ||
            string.IsNullOrEmpty(request.ModelId) || string.IsNullOrEmpty(request.InputType) ||
            string.IsNullOrEmpty(request.InputData))
        {
            return false;
        }

        return request.InputType is "Text" or "Image" or "Json";
    }

    private bool ValidateModelConfig(AIModelConfig config)
    {
        if (string.IsNullOrEmpty(config.Name) || string.IsNullOrEmpty(config.Type) ||
            string.IsNullOrEmpty(config.InputType) || string.IsNullOrEmpty(config.OutputType))
        {
            return false;
        }

        if (config.Type == "LocalML" && string.IsNullOrEmpty(config.FilePath))
        {
            return false;
        }

        if ((config.Type == "ExternalAPI" || config.Type == "MCP") && string.IsNullOrEmpty(config.ExternalApiUrl))
        {
            return false;
        }

        return config.InputType is "Text" or "Image" or "Json" &&
               config.OutputType is "Text" or "Json";
    }

    private async Task<string> InvokeLocalMLModel(AIModel model, string inputData)
    {
        try
        {
            using var stream = File.OpenRead(model.FilePath);
            var mlModel = _mlContext.Model.Load(stream, out var schema);

            // Create input object
            var input = new MLInput();
            if (model.InputType == "Text")
            {
                input.InputText = inputData;
            }
            else if (model.InputType == "Image")
            {
                input.InputImage = Convert.FromBase64String(inputData);
            }
            else if (model.InputType == "Json")
            {
                input.InputJson = JsonSerializer.Deserialize<JsonElement>(inputData).ToString();
            }

            // Create prediction engine
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<MLInput, MLOutput>(mlModel);

            // Make prediction
            var prediction = predictionEngine.Predict(input);

            // Format output
            string output = model.OutputType == "Json"
                ? JsonSerializer.Serialize(new { prediction.OutputText, prediction.OutputJson })
                : prediction.OutputText ?? prediction.OutputJson ?? string.Empty;

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking local ML model {ModelId}", model.Id);
            throw;
        }
    }

    private async Task<string> InvokeExternalAPI(AIModel model, string inputData, string modelName = "")
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Post, model.ExternalApiUrl);
            if (!string.IsNullOrEmpty(model.ExternalApiKey))
            {
                var decryptedApiKey = _protector.Unprotect(model.ExternalApiKey);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", decryptedApiKey);
            }

            var payload = new { inputs = inputData };
            // DeMCP chat completions format
            /*var payload = new
            {
                model = string.IsNullOrEmpty(modelName) ? model.ModelName : modelName,
                messages = new[] { new { role = "user", content = inputData } },
                stream = false
            };*/
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var output = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received external API response for model {ModelId}: {Response}", model.Id, output);

            if (model.OutputType == "Json")
            {
                return output; // Return raw JSON if OutputType is Json
            }

            // Parse the response as a JSON element
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(output);

            // Handle array response (common for Hugging Face models)
            if (jsonElement.ValueKind == JsonValueKind.Array && jsonElement.GetArrayLength() > 0)
            {
                var firstElement = jsonElement[0];
                if (firstElement.ValueKind == JsonValueKind.Object && firstElement.TryGetProperty("generated_text", out var generatedText))
                {
                    return generatedText.GetString() ?? string.Empty;
                }
                _logger.LogWarning("No 'generated_text' property found in array response for model {ModelId}", model.Id);
                return string.Empty;
            }

            // Handle single string response
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return jsonElement.GetString() ?? string.Empty;
            }

            _logger.LogWarning("Unexpected response format for model {ModelId}: {Response}", model.Id, output);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking external API for model {ModelId}", model.Id);
            throw;
        }
    }

private async Task<string> InvokeMCPModel(AIModel model, string inputData, string transport, string modelName = "")
{
    try
    {
        if (transport != "StreamableHTTP")
        {
            _logger.LogWarning("Unsupported MCP transport {Transport} for Model {ModelId}", transport, model.Id);
            throw new ArgumentException("Only StreamableHTTP is supported");
        }

        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, model.ExternalApiUrl);
        
        // Add debug logging
        _logger.LogDebug("Invoking MCP model at: {Url}", model.ExternalApiUrl);
        _logger.LogDebug("Payload model: {Model}", string.IsNullOrEmpty(modelName) ? model.ModelName : modelName);

        if (!string.IsNullOrEmpty(model.ExternalApiKey))
        {
            // DECRYPT the API key first
            var decryptedApiKey = _protector.Unprotect(model.ExternalApiKey);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", decryptedApiKey);
        }

        var payload = new
        {
            model = string.IsNullOrEmpty(modelName) ? model.ModelName : modelName,
            messages = new[] { new { role = "user", content = inputData } },
            stream = false,
            context = new { source = "Prismon", userId = model.AppId }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        // Add timeout and debug logging
        client.Timeout = TimeSpan.FromSeconds(30);
        _logger.LogDebug("Sending request: {Request}", jsonPayload);

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        
        // First verify we got a successful response
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("MCP API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"MCP API returned {response.StatusCode}: {errorContent}");
        }

        // Handle both streaming and non-streaming responses
        var content = await response.Content.ReadAsStringAsync();
        
        // Case 1: Regular JSON response
        if (!content.Contains("data: "))
        {
            _logger.LogDebug("Received non-streaming response: {Response}", content);
            
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(content);
                if (model.OutputType == "Json") return content;
                
                return json.GetProperty("choices")[0]
                          .GetProperty("message")
                          .GetProperty("content")
                          .GetString() ?? string.Empty;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse MCP response: {Content}", content);
                throw new InvalidOperationException("Invalid JSON response from MCP service");
            }
        }

        // Case 2: Streaming response (SSE format)
        _logger.LogDebug("Received streaming response");
        var output = new StringBuilder();
        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var reader = new StreamReader(stream))
        {
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line?.StartsWith("data: ") == true)
                {
                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]") continue;
                    output.Append(data);
                }
            }
        }

        var result = output.ToString();
        if (string.IsNullOrEmpty(result))
        {
            throw new InvalidOperationException("Received empty streaming response");
        }

        if (model.OutputType == "Json") return result;
        
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(result)
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse streaming response: {Result}", result);
            throw new InvalidOperationException("Invalid streaming JSON response");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error invoking MCP model {ModelId}", model.Id);
        throw;
    }
}

    // Helper class for dynamic ML.NET inputs/outputs
    private class DynamicData
    {
        private readonly Dictionary<string, object> _data = new();

        public DynamicData(MLContext mlContext, DataViewSchema schema)
        {
            foreach (var column in schema)
            {
                _data[column.Name] = null;
            }
        }

        public object this[string key]
        {
            get => _data[key];
            set => _data[key] = value;
        }
    }
}
// Classes for ML.NET input/output
public class MLInput
{
    public string InputText { get; set; } = string.Empty;
    public byte[] InputImage { get; set; } = Array.Empty<byte>();
    public string InputJson { get; set; } = string.Empty;
}

public class MLOutput
{
    public string OutputText { get; set; } = string.Empty;
    public string OutputJson { get; set; } = string.Empty;
}