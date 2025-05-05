using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayStack.Net;

namespace Prismon.Api.Services
{
    public interface IPaymentService
    {
        Task<string> InitializePaymentAsync(string email, string tier, decimal amount, string currency, string reference);
        Task<bool> VerifyPaymentAsync(string reference);
    }

    public class PaymentService : IPaymentService
    {
        private readonly PayStackApi _paystack;
        private readonly ILogger<PaymentService> _logger;
        private readonly PaystackConfig _config;

        public PaymentService(IOptions<PaystackConfig> config, ILogger<PaymentService> logger)
        {
            _config = config.Value;
            _logger = logger;

            if (string.IsNullOrWhiteSpace("sk_test_11fba9b23756b99d7c5bdbb56131bf6e329c39c7"))
                throw new InvalidOperationException("Paystack secret key is missing from configuration.");

            _paystack = new PayStackApi("sk_test_11fba9b23756b99d7c5bdbb56131bf6e329c39c7");
            
        }

        public async Task<string> InitializePaymentAsync(string email, string tier, decimal amount, string currency, string reference)
        {
            try
            {
                var request = new TransactionInitializeRequest
                {
                    Email = email,
                    AmountInKobo = Convert.ToInt32(amount * 100),
                    Currency = currency,
                    Reference = reference,
                    CallbackUrl = _config.CallbackUrl, // Should be provided in config
                    //Metadata = new { tier }
                };

                var response = _paystack.Transactions.Initialize(request);

                if (response == null || !response.Status || response.Data == null)
                    throw new Exception($"Paystack initialization failed: {response?.Message ?? "Unknown error"}");

                _logger.LogInformation("Payment initialized for {Email}, Tier: {Tier}, Reference: {Reference}", email, tier, reference);
                return response.Data.AuthorizationUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize payment for {Email}, Tier: {Tier}", email, tier);
                throw;
            }
        }

        public async Task<bool> VerifyPaymentAsync(string reference)
        {
            try
            {
                var response = _paystack.Transactions.Verify(reference);

                if (response == null || !response.Status || response.Data?.Status != "success")
                {
                    _logger.LogWarning("Payment verification failed for Reference: {Reference}, Status: {Status}", reference, response.Data?.Status);
                    return false;
                }

                _logger.LogInformation("Payment verified for Reference: {Reference}", reference);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify payment for Reference: {Reference}", reference);
                return false;
            }
        }
    }

    public class PaystackConfig
    {
        public string SecretKey { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty; // NEW: to avoid hardcoded URL
        public decimal PremiumPlanPriceUSD { get; set; }
        public decimal PremiumPlanPriceNGN { get; set; }
    }
}
