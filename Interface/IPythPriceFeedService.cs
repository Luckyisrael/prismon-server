namespace Prismon.Api.Interface;
public interface IPythPriceFeedService
{
    Task<List<PriceFeedInfo>> GetPriceFeedsAsync(PriceFeedRequest request);
    Task<List<PriceUpdate>> GetLatestPriceAsync(LatestPriceRequest request);
    Task StartPriceStreamAsync(string sessionId, StreamPriceRequest request, Func<PriceUpdate, Task> onPriceUpdate);
    Task StopPriceStreamAsync(string sessionId);
}