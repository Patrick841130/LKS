using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LksBrothers.Core.Models;

namespace LksBrothers.Ecosystem.Services
{
    public class EcosystemIntegrationService
    {
        private readonly ILogger<EcosystemIntegrationService> _logger;
        private readonly EcosystemConfiguration _config;
        private readonly IUniversalPaymentService _paymentService;
        private readonly IServiceRegistry _serviceRegistry;

        public EcosystemIntegrationService(
            ILogger<EcosystemIntegrationService> logger,
            IOptions<EcosystemConfiguration> config,
            IUniversalPaymentService paymentService,
            IServiceRegistry serviceRegistry)
        {
            _logger = logger;
            _config = config.Value;
            _paymentService = paymentService;
            _serviceRegistry = serviceRegistry;
        }

        /// <summary>
        /// Process payment for IP Patent services
        /// </summary>
        public async Task<PaymentResult> ProcessIPPatentPayment(IPPatentPaymentRequest request)
        {
            try
            {
                var paymentRequest = new UniversalPaymentRequest
                {
                    ServiceType = ServiceType.IP_PATENT,
                    Amount = request.Amount,
                    UserAddress = request.UserAddress,
                    ReferenceId = $"PATENT_{request.PatentApplicationId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["patent_type"] = request.PatentType,
                        ["application_id"] = request.PatentApplicationId,
                        ["filing_country"] = request.FilingCountry,
                        ["priority_date"] = request.PriorityDate
                    }
                };

                var result = await _paymentService.ProcessPayment(paymentRequest);
                
                if (result.Success)
                {
                    // Trigger patent filing process
                    await NotifyIPPatentService(request, result.TransactionHash);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing IP Patent payment for application {ApplicationId}", 
                    request.PatentApplicationId);
                throw;
            }
        }

        /// <summary>
        /// Process payment for LKS Summit tickets and booths
        /// </summary>
        public async Task<PaymentResult> ProcessLKSSummitPayment(LKSSummitPaymentRequest request)
        {
            try
            {
                var paymentRequest = new UniversalPaymentRequest
                {
                    ServiceType = ServiceType.LKS_SUMMIT,
                    Amount = request.Amount,
                    UserAddress = request.UserAddress,
                    ReferenceId = $"SUMMIT_{request.EventId}_{request.ItemType}_{request.ItemId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["event_id"] = request.EventId,
                        ["item_type"] = request.ItemType, // "ticket" or "booth"
                        ["item_id"] = request.ItemId,
                        ["attendee_info"] = request.AttendeeInfo,
                        ["special_requirements"] = request.SpecialRequirements
                    }
                };

                var result = await _paymentService.ProcessPayment(paymentRequest);
                
                if (result.Success)
                {
                    // Generate NFT ticket or booth reservation
                    await GenerateEventNFT(request, result.TransactionHash);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LKS Summit payment for event {EventId}", 
                    request.EventId);
                throw;
            }
        }

        /// <summary>
        /// Process payment for Software Factory services
        /// </summary>
        public async Task<PaymentResult> ProcessSoftwareFactoryPayment(SoftwareFactoryPaymentRequest request)
        {
            try
            {
                var paymentRequest = new UniversalPaymentRequest
                {
                    ServiceType = ServiceType.SOFTWARE_FACTORY,
                    Amount = request.Amount,
                    UserAddress = request.UserAddress,
                    ReferenceId = $"SOFTWARE_{request.ProjectId}_{request.MilestoneId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["project_id"] = request.ProjectId,
                        ["milestone_id"] = request.MilestoneId,
                        ["project_type"] = request.ProjectType,
                        ["payment_type"] = request.PaymentType, // "milestone", "subscription", "full"
                        ["deliverables"] = request.Deliverables
                    }
                };

                var result = await _paymentService.ProcessPayment(paymentRequest);
                
                if (result.Success)
                {
                    // Update project milestone status
                    await UpdateProjectMilestone(request, result.TransactionHash);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Software Factory payment for project {ProjectId}", 
                    request.ProjectId);
                throw;
            }
        }

        /// <summary>
        /// Process payment for Vara cybersecurity services
        /// </summary>
        public async Task<PaymentResult> ProcessVaraSecurityPayment(VaraSecurityPaymentRequest request)
        {
            try
            {
                var paymentRequest = new UniversalPaymentRequest
                {
                    ServiceType = ServiceType.VARA_SECURITY,
                    Amount = request.Amount,
                    UserAddress = request.UserAddress,
                    ReferenceId = $"SECURITY_{request.ServiceType}_{request.AssessmentId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["assessment_id"] = request.AssessmentId,
                        ["security_service_type"] = request.ServiceType,
                        ["target_systems"] = request.TargetSystems,
                        ["urgency_level"] = request.UrgencyLevel,
                        ["compliance_requirements"] = request.ComplianceRequirements
                    }
                };

                var result = await _paymentService.ProcessPayment(paymentRequest);
                
                if (result.Success)
                {
                    // Schedule security assessment
                    await ScheduleSecurityAssessment(request, result.TransactionHash);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Vara Security payment for assessment {AssessmentId}", 
                    request.AssessmentId);
                throw;
            }
        }

        /// <summary>
        /// Process payment for Stadium Tackle gaming
        /// </summary>
        public async Task<PaymentResult> ProcessStadiumTacklePayment(StadiumTacklePaymentRequest request)
        {
            try
            {
                var paymentRequest = new UniversalPaymentRequest
                {
                    ServiceType = ServiceType.STADIUM_TACKLE,
                    Amount = request.Amount,
                    UserAddress = request.UserAddress,
                    ReferenceId = $"GAME_{request.TransactionType}_{request.ItemId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["transaction_type"] = request.TransactionType, // "purchase", "tournament", "upgrade"
                        ["item_id"] = request.ItemId,
                        ["game_mode"] = request.GameMode,
                        ["tournament_id"] = request.TournamentId,
                        ["player_level"] = request.PlayerLevel
                    }
                };

                var result = await _paymentService.ProcessPayment(paymentRequest);
                
                if (result.Success)
                {
                    // Process in-game transaction
                    await ProcessGameTransaction(request, result.TransactionHash);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Stadium Tackle payment for transaction {TransactionType}", 
                    request.TransactionType);
                throw;
            }
        }

        /// <summary>
        /// Process payment for LKS Capital crowdfunding
        /// </summary>
        public async Task<PaymentResult> ProcessLKSCapitalPayment(LKSCapitalPaymentRequest request)
        {
            try
            {
                var paymentRequest = new UniversalPaymentRequest
                {
                    ServiceType = ServiceType.LKS_CAPITAL,
                    Amount = request.Amount,
                    UserAddress = request.UserAddress,
                    ReferenceId = $"FUNDING_{request.CampaignId}_{request.InvestmentId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["campaign_id"] = request.CampaignId,
                        ["investment_id"] = request.InvestmentId,
                        ["investment_type"] = request.InvestmentType,
                        ["expected_return"] = request.ExpectedReturn,
                        ["risk_level"] = request.RiskLevel
                    }
                };

                var result = await _paymentService.ProcessPayment(paymentRequest);
                
                if (result.Success)
                {
                    // Record investment and issue tokens
                    await ProcessInvestment(request, result.TransactionHash);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LKS Capital payment for campaign {CampaignId}", 
                    request.CampaignId);
                throw;
            }
        }

        /// <summary>
        /// Get user's ecosystem activity across all services
        /// </summary>
        public async Task<EcosystemUserActivity> GetUserActivity(string userAddress)
        {
            try
            {
                var activity = new EcosystemUserActivity
                {
                    UserAddress = userAddress,
                    TotalSpent = 0,
                    ServicesUsed = new List<ServiceUsage>(),
                    LoyaltyPoints = 0,
                    MembershipLevel = "Bronze"
                };

                // Get activity from each service
                var ipPatentActivity = await GetIPPatentActivity(userAddress);
                var summitActivity = await GetLKSSummitActivity(userAddress);
                var softwareActivity = await GetSoftwareFactoryActivity(userAddress);
                var securityActivity = await GetVaraSecurityActivity(userAddress);
                var gamingActivity = await GetStadiumTackleActivity(userAddress);
                var capitalActivity = await GetLKSCapitalActivity(userAddress);

                activity.ServicesUsed.AddRange(new[]
                {
                    ipPatentActivity, summitActivity, softwareActivity,
                    securityActivity, gamingActivity, capitalActivity
                });

                // Calculate totals
                activity.TotalSpent = activity.ServicesUsed.Sum(s => s.TotalSpent);
                activity.LoyaltyPoints = CalculateLoyaltyPoints(activity.ServicesUsed);
                activity.MembershipLevel = DetermineMembershipLevel(activity.TotalSpent, activity.ServicesUsed.Count);

                return activity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user activity for {UserAddress}", userAddress);
                throw;
            }
        }

        /// <summary>
        /// Get ecosystem-wide statistics
        /// </summary>
        public async Task<EcosystemStatistics> GetEcosystemStatistics()
        {
            try
            {
                var stats = new EcosystemStatistics
                {
                    TotalUsers = await _paymentService.GetTotalUsers(),
                    TotalTransactions = await _paymentService.GetTotalTransactions(),
                    TotalVolume = await _paymentService.GetTotalVolume(),
                    ServiceStatistics = new Dictionary<ServiceType, ServiceStatistics>()
                };

                // Get statistics for each service
                foreach (ServiceType serviceType in Enum.GetValues<ServiceType>())
                {
                    var serviceStats = await _paymentService.GetServiceStatistics(serviceType);
                    stats.ServiceStatistics[serviceType] = serviceStats;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ecosystem statistics");
                throw;
            }
        }

        // Private helper methods
        private async Task NotifyIPPatentService(IPPatentPaymentRequest request, string transactionHash)
        {
            // Integration with IP Patent service
            await _serviceRegistry.NotifyService(ServiceType.IP_PATENT, new
            {
                action = "payment_received",
                patent_application_id = request.PatentApplicationId,
                transaction_hash = transactionHash,
                amount = request.Amount
            });
        }

        private async Task GenerateEventNFT(LKSSummitPaymentRequest request, string transactionHash)
        {
            // Generate NFT ticket or booth reservation
            await _serviceRegistry.NotifyService(ServiceType.LKS_SUMMIT, new
            {
                action = "generate_nft",
                event_id = request.EventId,
                item_type = request.ItemType,
                transaction_hash = transactionHash,
                user_address = request.UserAddress
            });
        }

        private async Task UpdateProjectMilestone(SoftwareFactoryPaymentRequest request, string transactionHash)
        {
            // Update project milestone status
            await _serviceRegistry.NotifyService(ServiceType.SOFTWARE_FACTORY, new
            {
                action = "milestone_payment",
                project_id = request.ProjectId,
                milestone_id = request.MilestoneId,
                transaction_hash = transactionHash
            });
        }

        private async Task ScheduleSecurityAssessment(VaraSecurityPaymentRequest request, string transactionHash)
        {
            // Schedule security assessment
            await _serviceRegistry.NotifyService(ServiceType.VARA_SECURITY, new
            {
                action = "schedule_assessment",
                assessment_id = request.AssessmentId,
                transaction_hash = transactionHash,
                urgency_level = request.UrgencyLevel
            });
        }

        private async Task ProcessGameTransaction(StadiumTacklePaymentRequest request, string transactionHash)
        {
            // Process in-game transaction
            await _serviceRegistry.NotifyService(ServiceType.STADIUM_TACKLE, new
            {
                action = "process_transaction",
                transaction_type = request.TransactionType,
                item_id = request.ItemId,
                transaction_hash = transactionHash,
                user_address = request.UserAddress
            });
        }

        private async Task ProcessInvestment(LKSCapitalPaymentRequest request, string transactionHash)
        {
            // Record investment and issue tokens
            await _serviceRegistry.NotifyService(ServiceType.LKS_CAPITAL, new
            {
                action = "process_investment",
                campaign_id = request.CampaignId,
                investment_id = request.InvestmentId,
                transaction_hash = transactionHash,
                amount = request.Amount
            });
        }

        private async Task<ServiceUsage> GetIPPatentActivity(string userAddress)
        {
            // Get IP Patent service activity
            return new ServiceUsage
            {
                ServiceType = ServiceType.IP_PATENT,
                ServiceName = "IP Patent Services",
                TransactionCount = await _paymentService.GetUserTransactionCount(userAddress, ServiceType.IP_PATENT),
                TotalSpent = await _paymentService.GetUserTotalSpent(userAddress, ServiceType.IP_PATENT),
                LastActivity = await _paymentService.GetUserLastActivity(userAddress, ServiceType.IP_PATENT)
            };
        }

        private async Task<ServiceUsage> GetLKSSummitActivity(string userAddress)
        {
            return new ServiceUsage
            {
                ServiceType = ServiceType.LKS_SUMMIT,
                ServiceName = "LKS Summit Events",
                TransactionCount = await _paymentService.GetUserTransactionCount(userAddress, ServiceType.LKS_SUMMIT),
                TotalSpent = await _paymentService.GetUserTotalSpent(userAddress, ServiceType.LKS_SUMMIT),
                LastActivity = await _paymentService.GetUserLastActivity(userAddress, ServiceType.LKS_SUMMIT)
            };
        }

        private async Task<ServiceUsage> GetSoftwareFactoryActivity(string userAddress)
        {
            return new ServiceUsage
            {
                ServiceType = ServiceType.SOFTWARE_FACTORY,
                ServiceName = "Software Factory",
                TransactionCount = await _paymentService.GetUserTransactionCount(userAddress, ServiceType.SOFTWARE_FACTORY),
                TotalSpent = await _paymentService.GetUserTotalSpent(userAddress, ServiceType.SOFTWARE_FACTORY),
                LastActivity = await _paymentService.GetUserLastActivity(userAddress, ServiceType.SOFTWARE_FACTORY)
            };
        }

        private async Task<ServiceUsage> GetVaraSecurityActivity(string userAddress)
        {
            return new ServiceUsage
            {
                ServiceType = ServiceType.VARA_SECURITY,
                ServiceName = "Vara Cybersecurity",
                TransactionCount = await _paymentService.GetUserTransactionCount(userAddress, ServiceType.VARA_SECURITY),
                TotalSpent = await _paymentService.GetUserTotalSpent(userAddress, ServiceType.VARA_SECURITY),
                LastActivity = await _paymentService.GetUserLastActivity(userAddress, ServiceType.VARA_SECURITY)
            };
        }

        private async Task<ServiceUsage> GetStadiumTackleActivity(string userAddress)
        {
            return new ServiceUsage
            {
                ServiceType = ServiceType.STADIUM_TACKLE,
                ServiceName = "Stadium Tackle Gaming",
                TransactionCount = await _paymentService.GetUserTransactionCount(userAddress, ServiceType.STADIUM_TACKLE),
                TotalSpent = await _paymentService.GetUserTotalSpent(userAddress, ServiceType.STADIUM_TACKLE),
                LastActivity = await _paymentService.GetUserLastActivity(userAddress, ServiceType.STADIUM_TACKLE)
            };
        }

        private async Task<ServiceUsage> GetLKSCapitalActivity(string userAddress)
        {
            return new ServiceUsage
            {
                ServiceType = ServiceType.LKS_CAPITAL,
                ServiceName = "LKS Capital Crowdfunding",
                TransactionCount = await _paymentService.GetUserTransactionCount(userAddress, ServiceType.LKS_CAPITAL),
                TotalSpent = await _paymentService.GetUserTotalSpent(userAddress, ServiceType.LKS_CAPITAL),
                LastActivity = await _paymentService.GetUserLastActivity(userAddress, ServiceType.LKS_CAPITAL)
            };
        }

        private int CalculateLoyaltyPoints(List<ServiceUsage> servicesUsed)
        {
            // 1 point per LKS COIN spent + bonus for using multiple services
            var basePoints = (int)servicesUsed.Sum(s => s.TotalSpent);
            var serviceBonus = servicesUsed.Count(s => s.TransactionCount > 0) * 100;
            return basePoints + serviceBonus;
        }

        private string DetermineMembershipLevel(decimal totalSpent, int servicesUsed)
        {
            if (totalSpent >= 100000 && servicesUsed >= 5) return "Platinum";
            if (totalSpent >= 50000 && servicesUsed >= 4) return "Gold";
            if (totalSpent >= 10000 && servicesUsed >= 3) return "Silver";
            return "Bronze";
        }
    }
}
