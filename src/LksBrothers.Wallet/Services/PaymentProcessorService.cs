using Microsoft.Extensions.Logging;
using Stripe;
using System.Text.Json;

namespace LksBrothers.Wallet.Services
{
    public interface IPaymentProcessorService
    {
        Task<PaymentResult> ProcessCardPaymentAsync(CardPaymentRequest request);
        Task<SubscriptionResult> CreateSubscriptionAsync(SubscriptionRequest request);
        Task<bool> ProcessSubscriptionPaymentAsync(string subscriptionId, decimal amount);
        Task<List<PaymentMethod>> GetUserPaymentMethodsAsync(string userId);
        Task<PaymentMethod> AddPaymentMethodAsync(string userId, CardDetails cardDetails);
    }

    public class PaymentProcessorService : IPaymentProcessorService
    {
        private readonly ILogger<PaymentProcessorService> _logger;
        private readonly ILksWalletService _walletService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly PaymentIntentService _paymentIntentService;
        private readonly PaymentMethodService _paymentMethodService;
        private readonly SubscriptionService _stripeSubscriptionService;
        private readonly CustomerService _customerService;

        public PaymentProcessorService(
            ILogger<PaymentProcessorService> logger,
            ILksWalletService walletService,
            ISubscriptionService subscriptionService)
        {
            _logger = logger;
            _walletService = walletService;
            _subscriptionService = subscriptionService;
            
            // Initialize Stripe services
            StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
            _paymentIntentService = new PaymentIntentService();
            _paymentMethodService = new PaymentMethodService();
            _stripeSubscriptionService = new SubscriptionService();
            _customerService = new CustomerService();
        }

        public async Task<PaymentResult> ProcessCardPaymentAsync(CardPaymentRequest request)
        {
            try
            {
                _logger.LogInformation($"Processing card payment for user: {request.UserId}, Amount: ${request.AmountUsd}");

                // Create or get Stripe customer
                var customer = await GetOrCreateStripeCustomerAsync(request.UserId, request.Email);

                // Create payment intent
                var paymentIntentOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)(request.AmountUsd * 100), // Convert to cents
                    Currency = "usd",
                    Customer = customer.Id,
                    PaymentMethod = request.PaymentMethodId,
                    ConfirmationMethod = "manual",
                    Confirm = true,
                    Description = $"LKS COIN Purchase - {request.LksAmount} LKS",
                    Metadata = new Dictionary<string, string>
                    {
                        ["user_id"] = request.UserId,
                        ["lks_amount"] = request.LksAmount.ToString(),
                        ["service"] = request.Service ?? "lks_purchase"
                    }
                };

                var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions);

                if (paymentIntent.Status == "succeeded")
                {
                    // Credit LKS coins to user's wallet
                    await _walletService.CreditCoinsAsync(request.UserId, request.LksAmount, "Card Purchase");

                    // Record transaction
                    await RecordPaymentTransactionAsync(new PaymentTransaction
                    {
                        UserId = request.UserId,
                        PaymentIntentId = paymentIntent.Id,
                        AmountUsd = request.AmountUsd,
                        LksAmount = request.LksAmount,
                        Status = "completed",
                        PaymentMethod = "card",
                        Service = request.Service,
                        Timestamp = DateTime.UtcNow
                    });

                    return new PaymentResult
                    {
                        Success = true,
                        TransactionId = paymentIntent.Id,
                        Status = "completed",
                        LksAmount = request.LksAmount,
                        Message = "Payment processed successfully"
                    };
                }
                else
                {
                    return new PaymentResult
                    {
                        Success = false,
                        Status = paymentIntent.Status,
                        ErrorMessage = "Payment requires additional authentication",
                        ClientSecret = paymentIntent.ClientSecret
                    };
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe payment error");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Status = "failed"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment processing error");
                return new PaymentResult
                {
                    Success = false,
                    ErrorMessage = "Payment processing failed",
                    Status = "failed"
                };
            }
        }

        public async Task<SubscriptionResult> CreateSubscriptionAsync(SubscriptionRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating subscription for user: {request.UserId}, Plan: {request.PlanType}");

                // Create or get Stripe customer
                var customer = await GetOrCreateStripeCustomerAsync(request.UserId, request.Email);

                // Attach payment method to customer
                await _paymentMethodService.AttachAsync(request.PaymentMethodId, new PaymentMethodAttachOptions
                {
                    Customer = customer.Id
                });

                // Calculate monthly payment amount
                decimal monthlyAmount = request.TotalAmountUsd / 12m;

                // Create Stripe subscription
                var subscriptionOptions = new SubscriptionCreateOptions
                {
                    Customer = customer.Id,
                    DefaultPaymentMethod = request.PaymentMethodId,
                    Items = new List<SubscriptionItemOptions>
                    {
                        new()
                        {
                            Price = await GetOrCreatePriceAsync(monthlyAmount, request.PlanType)
                        }
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["user_id"] = request.UserId,
                        ["plan_type"] = request.PlanType,
                        ["total_amount"] = request.TotalAmountUsd.ToString(),
                        ["lks_amount"] = request.LksAmount.ToString(),
                        ["service"] = "ip_patent_subscription"
                    }
                };

                var stripeSubscription = await _stripeSubscriptionService.CreateAsync(subscriptionOptions);

                // Create internal subscription record
                var subscription = new LksSubscription
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = request.UserId,
                    StripeSubscriptionId = stripeSubscription.Id,
                    PlanType = request.PlanType,
                    TotalAmountUsd = request.TotalAmountUsd,
                    MonthlyAmountUsd = monthlyAmount,
                    LksAmount = request.LksAmount,
                    Status = "active",
                    StartDate = DateTime.UtcNow,
                    NextPaymentDate = DateTime.UtcNow.AddMonths(1),
                    PaymentsRemaining = 12,
                    CoinsHeld = true,
                    CoinsReleased = false
                };

                await _subscriptionService.CreateSubscriptionAsync(subscription);

                // Hold LKS coins in escrow (user can use them but has payment obligation)
                await _walletService.HoldCoinsInEscrowAsync(request.UserId, request.LksAmount, subscription.Id);

                return new SubscriptionResult
                {
                    Success = true,
                    SubscriptionId = subscription.Id,
                    StripeSubscriptionId = stripeSubscription.Id,
                    MonthlyAmount = monthlyAmount,
                    NextPaymentDate = subscription.NextPaymentDate,
                    LksAmount = request.LksAmount,
                    Message = "Subscription created successfully. LKS coins are available for use."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription");
                return new SubscriptionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> ProcessSubscriptionPaymentAsync(string subscriptionId, decimal amount)
        {
            try
            {
                var subscription = await _subscriptionService.GetSubscriptionAsync(subscriptionId);
                if (subscription == null)
                {
                    _logger.LogError($"Subscription not found: {subscriptionId}");
                    return false;
                }

                // Process payment through Stripe
                var stripeSubscription = await _stripeSubscriptionService.GetAsync(subscription.StripeSubscriptionId);
                
                // Update subscription record
                subscription.PaymentsRemaining--;
                subscription.NextPaymentDate = DateTime.UtcNow.AddMonths(1);
                
                if (subscription.PaymentsRemaining <= 0)
                {
                    subscription.Status = "completed";
                    subscription.CoinsReleased = true;
                    
                    // Release coins from escrow
                    await _walletService.ReleaseCoinsFromEscrowAsync(subscription.UserId, subscription.Id);
                }

                await _subscriptionService.UpdateSubscriptionAsync(subscription);

                _logger.LogInformation($"Subscription payment processed: {subscriptionId}, Remaining: {subscription.PaymentsRemaining}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing subscription payment: {subscriptionId}");
                return false;
            }
        }

        public async Task<List<PaymentMethod>> GetUserPaymentMethodsAsync(string userId)
        {
            try
            {
                var customer = await GetStripeCustomerAsync(userId);
                if (customer == null) return new List<PaymentMethod>();

                var paymentMethods = await _paymentMethodService.ListAsync(new PaymentMethodListOptions
                {
                    Customer = customer.Id,
                    Type = "card"
                });

                return paymentMethods.Data.Select(pm => new PaymentMethod
                {
                    Id = pm.Id,
                    Type = pm.Type,
                    CardBrand = pm.Card?.Brand,
                    CardLast4 = pm.Card?.Last4,
                    ExpiryMonth = pm.Card?.ExpMonth,
                    ExpiryYear = pm.Card?.ExpYear
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving payment methods for user: {userId}");
                return new List<PaymentMethod>();
            }
        }

        public async Task<PaymentMethod> AddPaymentMethodAsync(string userId, CardDetails cardDetails)
        {
            try
            {
                var customer = await GetOrCreateStripeCustomerAsync(userId, cardDetails.Email);

                var paymentMethodOptions = new PaymentMethodCreateOptions
                {
                    Type = "card",
                    Card = new PaymentMethodCardOptions
                    {
                        Number = cardDetails.Number,
                        ExpMonth = cardDetails.ExpiryMonth,
                        ExpYear = cardDetails.ExpiryYear,
                        Cvc = cardDetails.Cvc
                    }
                };

                var paymentMethod = await _paymentMethodService.CreateAsync(paymentMethodOptions);

                await _paymentMethodService.AttachAsync(paymentMethod.Id, new PaymentMethodAttachOptions
                {
                    Customer = customer.Id
                });

                return new PaymentMethod
                {
                    Id = paymentMethod.Id,
                    Type = paymentMethod.Type,
                    CardBrand = paymentMethod.Card?.Brand,
                    CardLast4 = paymentMethod.Card?.Last4,
                    ExpiryMonth = paymentMethod.Card?.ExpMonth,
                    ExpiryYear = paymentMethod.Card?.ExpYear
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding payment method for user: {userId}");
                throw;
            }
        }

        private async Task<Customer> GetOrCreateStripeCustomerAsync(string userId, string email)
        {
            var existingCustomer = await GetStripeCustomerAsync(userId);
            if (existingCustomer != null) return existingCustomer;

            var customerOptions = new CustomerCreateOptions
            {
                Email = email,
                Metadata = new Dictionary<string, string>
                {
                    ["user_id"] = userId
                }
            };

            return await _customerService.CreateAsync(customerOptions);
        }

        private async Task<Customer?> GetStripeCustomerAsync(string userId)
        {
            var customers = await _customerService.ListAsync(new CustomerListOptions
            {
                Limit = 1
            });

            return customers.Data.FirstOrDefault(c => 
                c.Metadata.ContainsKey("user_id") && c.Metadata["user_id"] == userId);
        }

        private async Task<string> GetOrCreatePriceAsync(decimal monthlyAmount, string planType)
        {
            // In production, you'd cache these or create them once
            var priceService = new PriceService();
            var productService = new ProductService();

            var product = await productService.CreateAsync(new ProductCreateOptions
            {
                Name = $"IP PATENT Subscription - {planType}",
                Description = "12-month IP PATENT service subscription"
            });

            var price = await priceService.CreateAsync(new PriceCreateOptions
            {
                Product = product.Id,
                UnitAmount = (long)(monthlyAmount * 100),
                Currency = "usd",
                Recurring = new PriceRecurringOptions
                {
                    Interval = "month"
                }
            });

            return price.Id;
        }

        private async Task RecordPaymentTransactionAsync(PaymentTransaction transaction)
        {
            // Implementation would save to database
            _logger.LogInformation($"Recording payment transaction: {JsonSerializer.Serialize(transaction)}");
        }
    }

    // Supporting models
    public class CardPaymentRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal AmountUsd { get; set; }
        public decimal LksAmount { get; set; }
        public string PaymentMethodId { get; set; } = string.Empty;
        public string? Service { get; set; }
    }

    public class SubscriptionRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PlanType { get; set; } = "ip_patent";
        public decimal TotalAmountUsd { get; set; } = 5000m;
        public decimal LksAmount { get; set; }
        public string PaymentMethodId { get; set; } = string.Empty;
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal LksAmount { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
    }

    public class SubscriptionResult
    {
        public bool Success { get; set; }
        public string SubscriptionId { get; set; } = string.Empty;
        public string StripeSubscriptionId { get; set; } = string.Empty;
        public decimal MonthlyAmount { get; set; }
        public DateTime NextPaymentDate { get; set; }
        public decimal LksAmount { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class PaymentMethod
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? CardBrand { get; set; }
        public string? CardLast4 { get; set; }
        public long? ExpiryMonth { get; set; }
        public long? ExpiryYear { get; set; }
    }

    public class CardDetails
    {
        public string Number { get; set; } = string.Empty;
        public long ExpiryMonth { get; set; }
        public long ExpiryYear { get; set; }
        public string Cvc { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class PaymentTransaction
    {
        public string UserId { get; set; } = string.Empty;
        public string PaymentIntentId { get; set; } = string.Empty;
        public decimal AmountUsd { get; set; }
        public decimal LksAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? Service { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class LksSubscription
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string StripeSubscriptionId { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public decimal TotalAmountUsd { get; set; }
        public decimal MonthlyAmountUsd { get; set; }
        public decimal LksAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime NextPaymentDate { get; set; }
        public int PaymentsRemaining { get; set; }
        public bool CoinsHeld { get; set; }
        public bool CoinsReleased { get; set; }
    }
}
