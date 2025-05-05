using Prismon.Api.Models;
namespace Prismon.Api.Interface;

public interface IAIService
{
    Task<AIInvokeResponse> InvokeAIAsync(AIInvokeRequest request);
    Task<RegisterModelResponse> RegisterModelAsync(string appId, AIModelConfig config);
}