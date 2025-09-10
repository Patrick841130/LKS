using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LksBrothers.Core.Models;
using LksBrothers.AI.Models;

namespace LksBrothers.AI.Services
{
    public class AIFraudDetectionEngine
    {
        private readonly ILogger<AIFraudDetectionEngine> _logger;
        private readonly AIConfiguration _config;
        private readonly IMLModelService _mlModelService;
        private readonly IBlockchainDataService _blockchainService;

        public AIFraudDetectionEngine(
            ILogger<AIFraudDetectionEngine> logger,
            IOptions<AIConfiguration> config,
            IMLModelService mlModelService,
            IBlockchainDataService blockchainService)
        {
            _logger = logger;
            _config = config.Value;
            _mlModelService = mlModelService;
            _blockchainService = blockchainService;
        }

        /// <summary>
        /// Real-time fraud detection for incoming transactions
        /// </summary>
        public async Task<SecurityAssessment> AnalyzeTransaction(Transaction transaction)
        {
            try
            {
                var startTime = DateTime.UtcNow;

                // Multi-layer fraud detection analysis
                var riskFactors = await AnalyzeRiskFactors(transaction);
                var behaviorAnalysis = await AnalyzeBehaviorPatterns(transaction);
                var networkAnalysis = await AnalyzeNetworkPatterns(transaction);
                var contractAnalysis = await AnalyzeSmartContractRisks(transaction);

                // Combine all analyses using ensemble model
                var overallAssessment = await CombineAssessments(
                    riskFactors, behaviorAnalysis, networkAnalysis, contractAnalysis);

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                _logger.LogDebug("Fraud analysis completed for transaction {TxHash} in {ProcessingTime}ms, Risk Score: {RiskScore}",
                    transaction.Hash, processingTime, overallAssessment.RiskScore);

                return overallAssessment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing transaction {TxHash} for fraud", transaction.Hash);
                
                // Return safe default assessment on error
                return new SecurityAssessment
                {
                    RiskScore = 0.5, // Medium risk when uncertain
                    RecommendedAction = SecurityAction.Monitor,
                    ConfidenceLevel = 0.1,
                    ThreatVectors = new List<ThreatVector>(),
                    ProcessingTime = 0
                };
            }
        }

        /// <summary>
        /// Analyzes basic risk factors using pattern recognition
        /// </summary>
        private async Task<RiskFactorAnalysis> AnalyzeRiskFactors(Transaction transaction)
        {
            var features = ExtractTransactionFeatures(transaction);
            
            var input = new MLModelInput
            {
                Data = features,
                ModelType = MLModelType.RiskAnalyzer
            };

            var prediction = await _mlModelService.PredictAsync(input);

            return new RiskFactorAnalysis
            {
                AmountRisk = prediction.GetFeature<double>("amount_risk"),
                FrequencyRisk = prediction.GetFeature<double>("frequency_risk"),
                AddressRisk = prediction.GetFeature<double>("address_risk"),
                TimingRisk = prediction.GetFeature<double>("timing_risk"),
                OverallRisk = prediction.GetFeature<double>("overall_risk"),
                Confidence = prediction.GetFeature<double>("confidence")
            };
        }

        /// <summary>
        /// Analyzes user behavior patterns for anomaly detection
        /// </summary>
        private async Task<BehaviorAnalysis> AnalyzeBehaviorPatterns(Transaction transaction)
        {
            // Get historical transactions for sender
            var historicalTxs = await _blockchainService.GetTransactionHistory(transaction.From, 100);
            
            var behaviorFeatures = new BehaviorFeatures
            {
                TransactionHistory = historicalTxs,
                CurrentTransaction = transaction,
                TimeWindow = TimeSpan.FromDays(30)
            };

            var input = new MLModelInput
            {
                Data = behaviorFeatures,
                ModelType = MLModelType.BehaviorAnalyzer
            };

            var prediction = await _mlModelService.PredictAsync(input);

            return new BehaviorAnalysis
            {
                IsAnomalous = prediction.GetFeature<bool>("is_anomalous"),
                AnomalyScore = prediction.GetFeature<double>("anomaly_score"),
                BehaviorPatterns = prediction.GetFeature<List<string>>("patterns"),
                DeviationFactors = prediction.GetFeature<List<string>>("deviations"),
                UserRiskProfile = prediction.GetFeature<string>("risk_profile")
            };
        }

        /// <summary>
        /// Analyzes network-level patterns and correlations
        /// </summary>
        private async Task<NetworkAnalysis> AnalyzeNetworkPatterns(Transaction transaction)
        {
            var networkFeatures = new NetworkFeatures
            {
                Transaction = transaction,
                RecentBlocks = await _blockchainService.GetRecentBlocks(10),
                NetworkMetrics = await _blockchainService.GetNetworkMetrics()
            };

            var input = new MLModelInput
            {
                Data = networkFeatures,
                ModelType = MLModelType.NetworkAnalyzer
            };

            var prediction = await _mlModelService.PredictAsync(input);

            return new NetworkAnalysis
            {
                ClusterRisk = prediction.GetFeature<double>("cluster_risk"),
                ConnectedAddresses = prediction.GetFeature<List<string>>("connected_addresses"),
                SuspiciousPatterns = prediction.GetFeature<List<string>>("suspicious_patterns"),
                NetworkPosition = prediction.GetFeature<string>("network_position"),
                InfluenceScore = prediction.GetFeature<double>("influence_score")
            };
        }

        /// <summary>
        /// Analyzes smart contract interactions for security risks
        /// </summary>
        private async Task<ContractSecurityAnalysis> AnalyzeSmartContractRisks(Transaction transaction)
        {
            if (transaction.To == null || transaction.Data == null || transaction.Data.Length == 0)
            {
                return new ContractSecurityAnalysis { IsContractInteraction = false };
            }

            var contractCode = await _blockchainService.GetContractCode(transaction.To);
            if (contractCode == null)
            {
                return new ContractSecurityAnalysis { IsContractInteraction = false };
            }

            var contractFeatures = new ContractSecurityFeatures
            {
                ContractAddress = transaction.To,
                ContractCode = contractCode,
                TransactionData = transaction.Data,
                Value = transaction.Value
            };

            var input = new MLModelInput
            {
                Data = contractFeatures,
                ModelType = MLModelType.ContractSecurityAnalyzer
            };

            var prediction = await _mlModelService.PredictAsync(input);

            return new ContractSecurityAnalysis
            {
                IsContractInteraction = true,
                SecurityRisk = prediction.GetFeature<double>("security_risk"),
                VulnerabilityTypes = prediction.GetFeature<List<string>>("vulnerabilities"),
                FunctionRisks = prediction.GetFeature<Dictionary<string, double>>("function_risks"),
                ReentrancyRisk = prediction.GetFeature<double>("reentrancy_risk"),
                OverflowRisk = prediction.GetFeature<double>("overflow_risk")
            };
        }

        /// <summary>
        /// Combines multiple assessments using ensemble learning
        /// </summary>
        private async Task<SecurityAssessment> CombineAssessments(
            RiskFactorAnalysis riskFactors,
            BehaviorAnalysis behavior,
            NetworkAnalysis network,
            ContractSecurityAnalysis contract)
        {
            var ensembleFeatures = new EnsembleFeatures
            {
                RiskFactors = riskFactors,
                Behavior = behavior,
                Network = network,
                Contract = contract
            };

            var input = new MLModelInput
            {
                Data = ensembleFeatures,
                ModelType = MLModelType.EnsembleClassifier
            };

            var prediction = await _mlModelService.PredictAsync(input);

            var riskScore = prediction.GetFeature<double>("risk_score");
            var threatVectors = prediction.GetFeature<List<ThreatVector>>("threat_vectors");
            var confidence = prediction.GetFeature<double>("confidence");

            return new SecurityAssessment
            {
                RiskScore = riskScore,
                RecommendedAction = DetermineAction(riskScore, confidence),
                ConfidenceLevel = confidence,
                ThreatVectors = threatVectors,
                DetailedAnalysis = new DetailedSecurityAnalysis
                {
                    RiskFactors = riskFactors,
                    BehaviorAnalysis = behavior,
                    NetworkAnalysis = network,
                    ContractAnalysis = contract
                },
                ProcessingTime = prediction.GetFeature<double>("processing_time")
            };
        }

        /// <summary>
        /// Determines recommended security action based on risk score
        /// </summary>
        private SecurityAction DetermineAction(double riskScore, double confidence)
        {
            if (confidence < 0.6) return SecurityAction.Monitor;
            
            return riskScore switch
            {
                >= 0.9 => SecurityAction.Block,
                >= 0.7 => SecurityAction.Quarantine,
                >= 0.5 => SecurityAction.FlagForReview,
                >= 0.3 => SecurityAction.Monitor,
                _ => SecurityAction.Allow
            };
        }

        /// <summary>
        /// Extracts relevant features from transaction for ML analysis
        /// </summary>
        private TransactionFeatures ExtractTransactionFeatures(Transaction transaction)
        {
            return new TransactionFeatures
            {
                Value = transaction.Value,
                GasPrice = transaction.GasPrice,
                GasLimit = transaction.GasLimit,
                FromAddress = transaction.From,
                ToAddress = transaction.To,
                DataSize = transaction.Data?.Length ?? 0,
                Timestamp = transaction.Timestamp,
                BlockNumber = transaction.BlockNumber ?? 0,
                TransactionIndex = transaction.TransactionIndex ?? 0,
                HasData = transaction.Data != null && transaction.Data.Length > 0,
                IsContractCreation = transaction.To == null
            };
        }

        /// <summary>
        /// Batch analysis for multiple transactions (used for block validation)
        /// </summary>
        public async Task<List<SecurityAssessment>> AnalyzeTransactionBatch(List<Transaction> transactions)
        {
            var tasks = transactions.Select(AnalyzeTransaction);
            return (await Task.WhenAll(tasks)).ToList();
        }

        /// <summary>
        /// Updates fraud detection models with new threat intelligence
        /// </summary>
        public async Task UpdateThreatIntelligence(ThreatIntelligenceUpdate update)
        {
            try
            {
                await _mlModelService.UpdateModel(MLModelType.RiskAnalyzer, update.RiskPatterns);
                await _mlModelService.UpdateModel(MLModelType.BehaviorAnalyzer, update.BehaviorPatterns);
                await _mlModelService.UpdateModel(MLModelType.NetworkAnalyzer, update.NetworkPatterns);
                
                _logger.LogInformation("Threat intelligence updated successfully with {PatternCount} new patterns",
                    update.RiskPatterns.Count + update.BehaviorPatterns.Count + update.NetworkPatterns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update threat intelligence");
            }
        }

        /// <summary>
        /// Gets fraud detection statistics and performance metrics
        /// </summary>
        public async Task<FraudDetectionMetrics> GetDetectionMetrics(TimeSpan timeWindow)
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime - timeWindow;

            var metrics = await _blockchainService.GetFraudDetectionMetrics(startTime, endTime);

            return new FraudDetectionMetrics
            {
                TotalTransactionsAnalyzed = metrics.TotalTransactions,
                FraudDetected = metrics.FraudDetected,
                FalsePositives = metrics.FalsePositives,
                FalseNegatives = metrics.FalseNegatives,
                AverageProcessingTime = metrics.AverageProcessingTime,
                AccuracyRate = CalculateAccuracy(metrics),
                PrecisionRate = CalculatePrecision(metrics),
                RecallRate = CalculateRecall(metrics),
                F1Score = CalculateF1Score(metrics)
            };
        }

        private double CalculateAccuracy(RawFraudMetrics metrics)
        {
            var total = metrics.TotalTransactions;
            var correct = total - metrics.FalsePositives - metrics.FalseNegatives;
            return total > 0 ? (double)correct / total : 0;
        }

        private double CalculatePrecision(RawFraudMetrics metrics)
        {
            var truePositives = metrics.FraudDetected - metrics.FalsePositives;
            var totalPositives = metrics.FraudDetected;
            return totalPositives > 0 ? (double)truePositives / totalPositives : 0;
        }

        private double CalculateRecall(RawFraudMetrics metrics)
        {
            var truePositives = metrics.FraudDetected - metrics.FalsePositives;
            var actualFraud = truePositives + metrics.FalseNegatives;
            return actualFraud > 0 ? (double)truePositives / actualFraud : 0;
        }

        private double CalculateF1Score(RawFraudMetrics metrics)
        {
            var precision = CalculatePrecision(metrics);
            var recall = CalculateRecall(metrics);
            return (precision + recall) > 0 ? 2 * (precision * recall) / (precision + recall) : 0;
        }
    }
}
