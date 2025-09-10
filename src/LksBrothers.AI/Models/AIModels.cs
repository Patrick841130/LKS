using System;
using System.Collections.Generic;

namespace LksBrothers.AI.Models
{
    // Configuration
    public class AIConfiguration
    {
        public string ModelEndpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int MaxConcurrentRequests { get; set; } = 100;
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableRealTimeOptimization { get; set; } = true;
        public double FraudDetectionThreshold { get; set; } = 0.7;
    }

    // ML Model Types
    public enum MLModelType
    {
        BytecodeAnalyzer,
        CodeGenerator,
        SafetyValidator,
        GasEstimator,
        SourceAnalyzer,
        QuickOptimizer,
        RiskAnalyzer,
        BehaviorAnalyzer,
        NetworkAnalyzer,
        ContractSecurityAnalyzer,
        EnsembleClassifier
    }

    // Smart Contract Optimization Models
    public class ContractOptimizationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public long OriginalGasCost { get; set; }
        public long OptimizedGasCost { get; set; }
        public double GasSavingsPercentage { get; set; }
        public byte[] OptimizedBytecode { get; set; } = Array.Empty<byte>();
        public List<string> OptimizationTechniques { get; set; } = new();
        public long PerformanceGains { get; set; }
        public double SafetyScore { get; set; }
        public List<string> SafetyIssues { get; set; } = new();
    }

    public class BytecodeAnalysis
    {
        public List<InstructionPattern> InstructionPatterns { get; set; } = new();
        public List<GasHotspot> GasHotspots { get; set; } = new();
        public List<OptimizationOpportunity> OptimizationOpportunities { get; set; } = new();
        public double ComplexityScore { get; set; }
        public List<SecurityRisk> SecurityRisks { get; set; } = new();
    }

    public class InstructionPattern
    {
        public PatternType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Frequency { get; set; }
        public long GasCost { get; set; }
    }

    public enum PatternType
    {
        Loop,
        FunctionCall,
        StorageAccess,
        ArithmeticOperation,
        ConditionalBranch
    }

    public class GasHotspot
    {
        public string Operation { get; set; } = string.Empty;
        public int Offset { get; set; }
        public long GasCost { get; set; }
        public string Suggestion { get; set; } = string.Empty;
    }

    public class OptimizationOpportunity
    {
        public OpportunityType Type { get; set; }
        public string Description { get; set; } = string.Empty;
        public long PotentialSavings { get; set; }
        public double Confidence { get; set; }
    }

    public enum OpportunityType
    {
        DeadCode,
        LoopOptimization,
        StorageOptimization,
        FunctionInlining,
        ConstantFolding
    }

    public class SecurityRisk
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RiskLevel Severity { get; set; }
        public string Mitigation { get; set; } = string.Empty;
    }

    public enum RiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class OptimizationTechnique
    {
        public string Technique { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long GasSavings { get; set; }
        public RiskLevel RiskLevel { get; set; }
    }

    public class SafetyValidation
    {
        public bool IsSafe { get; set; }
        public double SafetyScore { get; set; }
        public List<string> Issues { get; set; } = new();
        public bool FunctionalEquivalence { get; set; }
    }

    public class OptimizationSuggestion
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CodeExample { get; set; } = string.Empty;
        public long EstimatedGasSavings { get; set; }
        public string Difficulty { get; set; } = string.Empty;
    }

    public class QuickOptimizationResult
    {
        public byte[] OptimizedData { get; set; } = Array.Empty<byte>();
        public long GasSavings { get; set; }
        public double ConfidenceScore { get; set; }
    }

    // Fraud Detection Models
    public class SecurityAssessment
    {
        public double RiskScore { get; set; }
        public SecurityAction RecommendedAction { get; set; }
        public double ConfidenceLevel { get; set; }
        public List<ThreatVector> ThreatVectors { get; set; } = new();
        public DetailedSecurityAnalysis DetailedAnalysis { get; set; } = new();
        public double ProcessingTime { get; set; }
    }

    public enum SecurityAction
    {
        Allow,
        Monitor,
        FlagForReview,
        Quarantine,
        Block
    }

    public class ThreatVector
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Severity { get; set; }
        public string Mitigation { get; set; } = string.Empty;
    }

    public class DetailedSecurityAnalysis
    {
        public RiskFactorAnalysis RiskFactors { get; set; } = new();
        public BehaviorAnalysis BehaviorAnalysis { get; set; } = new();
        public NetworkAnalysis NetworkAnalysis { get; set; } = new();
        public ContractSecurityAnalysis ContractAnalysis { get; set; } = new();
    }

    public class RiskFactorAnalysis
    {
        public double AmountRisk { get; set; }
        public double FrequencyRisk { get; set; }
        public double AddressRisk { get; set; }
        public double TimingRisk { get; set; }
        public double OverallRisk { get; set; }
        public double Confidence { get; set; }
    }

    public class BehaviorAnalysis
    {
        public bool IsAnomalous { get; set; }
        public double AnomalyScore { get; set; }
        public List<string> BehaviorPatterns { get; set; } = new();
        public List<string> DeviationFactors { get; set; } = new();
        public string UserRiskProfile { get; set; } = string.Empty;
    }

    public class NetworkAnalysis
    {
        public double ClusterRisk { get; set; }
        public List<string> ConnectedAddresses { get; set; } = new();
        public List<string> SuspiciousPatterns { get; set; } = new();
        public string NetworkPosition { get; set; } = string.Empty;
        public double InfluenceScore { get; set; }
    }

    public class ContractSecurityAnalysis
    {
        public bool IsContractInteraction { get; set; }
        public double SecurityRisk { get; set; }
        public List<string> VulnerabilityTypes { get; set; } = new();
        public Dictionary<string, double> FunctionRisks { get; set; } = new();
        public double ReentrancyRisk { get; set; }
        public double OverflowRisk { get; set; }
    }

    // Feature Models for ML Input
    public class TransactionFeatures
    {
        public decimal Value { get; set; }
        public decimal GasPrice { get; set; }
        public long GasLimit { get; set; }
        public string FromAddress { get; set; } = string.Empty;
        public string ToAddress { get; set; } = string.Empty;
        public int DataSize { get; set; }
        public DateTime Timestamp { get; set; }
        public long BlockNumber { get; set; }
        public int TransactionIndex { get; set; }
        public bool HasData { get; set; }
        public bool IsContractCreation { get; set; }
    }

    public class BehaviorFeatures
    {
        public List<Transaction> TransactionHistory { get; set; } = new();
        public Transaction CurrentTransaction { get; set; } = new();
        public TimeSpan TimeWindow { get; set; }
    }

    public class NetworkFeatures
    {
        public Transaction Transaction { get; set; } = new();
        public List<Block> RecentBlocks { get; set; } = new();
        public NetworkMetrics NetworkMetrics { get; set; } = new();
    }

    public class ContractSecurityFeatures
    {
        public string ContractAddress { get; set; } = string.Empty;
        public byte[] ContractCode { get; set; } = Array.Empty<byte>();
        public byte[] TransactionData { get; set; } = Array.Empty<byte>();
        public decimal Value { get; set; }
    }

    public class EnsembleFeatures
    {
        public RiskFactorAnalysis RiskFactors { get; set; } = new();
        public BehaviorAnalysis Behavior { get; set; } = new();
        public NetworkAnalysis Network { get; set; } = new();
        public ContractSecurityAnalysis Contract { get; set; } = new();
    }

    // Threat Intelligence
    public class ThreatIntelligenceUpdate
    {
        public List<ThreatPattern> RiskPatterns { get; set; } = new();
        public List<ThreatPattern> BehaviorPatterns { get; set; } = new();
        public List<ThreatPattern> NetworkPatterns { get; set; } = new();
        public DateTime UpdateTimestamp { get; set; }
    }

    public class ThreatPattern
    {
        public string PatternId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, object> Features { get; set; } = new();
        public double Severity { get; set; }
        public List<string> Indicators { get; set; } = new();
    }

    // Metrics and Performance
    public class FraudDetectionMetrics
    {
        public long TotalTransactionsAnalyzed { get; set; }
        public long FraudDetected { get; set; }
        public long FalsePositives { get; set; }
        public long FalseNegatives { get; set; }
        public double AverageProcessingTime { get; set; }
        public double AccuracyRate { get; set; }
        public double PrecisionRate { get; set; }
        public double RecallRate { get; set; }
        public double F1Score { get; set; }
    }

    public class RawFraudMetrics
    {
        public long TotalTransactions { get; set; }
        public long FraudDetected { get; set; }
        public long FalsePositives { get; set; }
        public long FalseNegatives { get; set; }
        public double AverageProcessingTime { get; set; }
    }

    // ML Model Service Interface
    public interface IMLModelService
    {
        Task<MLModelPrediction> PredictAsync(MLModelInput input);
        Task UpdateModel(MLModelType modelType, List<ThreatPattern> patterns);
    }

    public class MLModelInput
    {
        public object Data { get; set; } = new();
        public MLModelType ModelType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class MLModelPrediction
    {
        public Dictionary<string, object> Features { get; set; } = new();
        public double Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }

        public T GetFeature<T>(string key)
        {
            if (Features.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default(T)!;
        }
    }

    // Blockchain Data Service Interface
    public interface IBlockchainDataService
    {
        Task<List<Transaction>> GetTransactionHistory(string address, int count);
        Task<List<Block>> GetRecentBlocks(int count);
        Task<NetworkMetrics> GetNetworkMetrics();
        Task<byte[]> GetContractCode(string address);
        Task<RawFraudMetrics> GetFraudDetectionMetrics(DateTime startTime, DateTime endTime);
    }

    public class NetworkMetrics
    {
        public long BlockHeight { get; set; }
        public double TransactionsPerSecond { get; set; }
        public int ActiveValidators { get; set; }
        public double NetworkHashRate { get; set; }
        public TimeSpan AverageBlockTime { get; set; }
        public long PendingTransactions { get; set; }
    }
}
