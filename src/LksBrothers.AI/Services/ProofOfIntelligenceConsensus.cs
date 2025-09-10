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
    /// <summary>
    /// Proof of Intelligence (PoI) consensus mechanism that combines staking with AI problem-solving
    /// Validators must solve AI challenges to participate in consensus, ensuring both security and intelligence
    /// </summary>
    public class ProofOfIntelligenceConsensus
    {
        private readonly ILogger<ProofOfIntelligenceConsensus> _logger;
        private readonly AIConfiguration _config;
        private readonly IMLModelService _mlModelService;
        private readonly IValidatorService _validatorService;
        private readonly IChallengeGenerator _challengeGenerator;

        public ProofOfIntelligenceConsensus(
            ILogger<ProofOfIntelligenceConsensus> logger,
            IOptions<AIConfiguration> config,
            IMLModelService mlModelService,
            IValidatorService validatorService,
            IChallengeGenerator challengeGenerator)
        {
            _logger = logger;
            _config = config.Value;
            _mlModelService = mlModelService;
            _validatorService = validatorService;
            _challengeGenerator = challengeGenerator;
        }

        /// <summary>
        /// Main consensus validation using Proof of Intelligence
        /// </summary>
        public async Task<ConsensusResult> ValidateBlock(Block block)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                
                _logger.LogInformation("Starting PoI consensus for block {BlockNumber}", block.Number);

                // Step 1: Generate AI challenge based on block complexity
                var aiChallenge = await GenerateBlockSpecificChallenge(block);
                
                // Step 2: Collect validator solutions
                var validatorResponses = await CollectValidatorSolutions(aiChallenge);
                
                // Step 3: Evaluate solutions and select optimal validators
                var selectedValidators = await SelectOptimalValidators(validatorResponses, block);
                
                // Step 4: Perform traditional consensus with selected validators
                var consensusResult = await FinalizeConsensus(selectedValidators, block);
                
                // Step 5: Update validator intelligence scores
                await UpdateValidatorIntelligenceScores(validatorResponses, consensusResult);

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                _logger.LogInformation("PoI consensus completed for block {BlockNumber} in {ProcessingTime}ms", 
                    block.Number, processingTime);

                return consensusResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PoI consensus for block {BlockNumber}", block.Number);
                
                // Fallback to traditional consensus
                return await FallbackConsensus(block);
            }
        }

        /// <summary>
        /// Generates AI challenge tailored to block complexity and network state
        /// </summary>
        private async Task<AIChallenge> GenerateBlockSpecificChallenge(Block block)
        {
            var challengeContext = new ChallengeContext
            {
                BlockNumber = block.Number,
                TransactionCount = block.Transactions.Count,
                BlockComplexity = CalculateBlockComplexity(block),
                NetworkState = await GetNetworkState(),
                PreviousBlockHash = block.ParentHash
            };

            var challengeType = DetermineChallengeType(challengeContext);
            
            return challengeType switch
            {
                ChallengeType.OptimizationChallenge => await _challengeGenerator.GenerateOptimizationChallenge(challengeContext),
                ChallengeType.SecurityAnalysis => await _challengeGenerator.GenerateSecurityChallenge(challengeContext),
                ChallengeType.PatternRecognition => await _challengeGenerator.GeneratePatternChallenge(challengeContext),
                ChallengeType.PredictiveModeling => await _challengeGenerator.GeneratePredictionChallenge(challengeContext),
                _ => await _challengeGenerator.GenerateGeneralChallenge(challengeContext)
            };
        }

        /// <summary>
        /// Collects solutions from all eligible validators within time limit
        /// </summary>
        private async Task<List<ValidatorResponse>> CollectValidatorSolutions(AIChallenge challenge)
        {
            var eligibleValidators = await _validatorService.GetEligibleValidators();
            var responses = new List<ValidatorResponse>();
            
            // Set challenge timeout based on difficulty
            var timeout = TimeSpan.FromSeconds(challenge.DifficultyLevel * 10);
            
            _logger.LogDebug("Collecting solutions from {ValidatorCount} validators with {Timeout}s timeout", 
                eligibleValidators.Count, timeout.TotalSeconds);

            var tasks = eligibleValidators.Select(async validator =>
            {
                try
                {
                    var solution = await RequestValidatorSolution(validator, challenge, timeout);
                    return new ValidatorResponse
                    {
                        Validator = validator,
                        Solution = solution,
                        SubmissionTime = DateTime.UtcNow,
                        IsValid = solution != null
                    };
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Validator {ValidatorId} timed out on challenge", validator.Id);
                    return new ValidatorResponse
                    {
                        Validator = validator,
                        Solution = null,
                        SubmissionTime = DateTime.UtcNow,
                        IsValid = false,
                        ErrorReason = "Timeout"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Validator {ValidatorId} failed challenge", validator.Id);
                    return new ValidatorResponse
                    {
                        Validator = validator,
                        Solution = null,
                        SubmissionTime = DateTime.UtcNow,
                        IsValid = false,
                        ErrorReason = ex.Message
                    };
                }
            });

            var allResponses = await Task.WhenAll(tasks);
            responses.AddRange(allResponses);

            _logger.LogInformation("Received {ValidResponses}/{TotalValidators} valid solutions", 
                responses.Count(r => r.IsValid), eligibleValidators.Count);

            return responses;
        }

        /// <summary>
        /// Evaluates validator solutions and selects the best performers for consensus
        /// </summary>
        private async Task<List<SelectedValidator>> SelectOptimalValidators(
            List<ValidatorResponse> responses, Block block)
        {
            var validResponses = responses.Where(r => r.IsValid).ToList();
            
            if (validResponses.Count == 0)
            {
                _logger.LogWarning("No valid validator responses received, falling back to stake-based selection");
                return await SelectByStakeOnly(responses.Select(r => r.Validator).ToList());
            }

            // Evaluate solution quality using AI
            var evaluatedResponses = await EvaluateSolutionQuality(validResponses);
            
            // Calculate composite scores (intelligence + stake + performance history)
            var scoredValidators = await CalculateCompositeScores(evaluatedResponses);
            
            // Select top validators based on composite scores
            var requiredValidators = CalculateRequiredValidatorCount(block);
            var selectedValidators = scoredValidators
                .OrderByDescending(v => v.CompositeScore)
                .Take(requiredValidators)
                .ToList();

            _logger.LogInformation("Selected {SelectedCount} validators from {CandidateCount} candidates", 
                selectedValidators.Count, scoredValidators.Count);

            return selectedValidators;
        }

        /// <summary>
        /// Evaluates the quality of validator solutions using AI models
        /// </summary>
        private async Task<List<EvaluatedResponse>> EvaluateSolutionQuality(List<ValidatorResponse> responses)
        {
            var evaluationTasks = responses.Select(async response =>
            {
                var input = new MLModelInput
                {
                    Data = new SolutionEvaluationData
                    {
                        Solution = response.Solution,
                        SubmissionTime = response.SubmissionTime,
                        ValidatorHistory = await _validatorService.GetValidatorHistory(response.Validator.Id)
                    },
                    ModelType = MLModelType.SolutionEvaluator
                };

                var evaluation = await _mlModelService.PredictAsync(input);
                
                return new EvaluatedResponse
                {
                    ValidatorResponse = response,
                    QualityScore = evaluation.GetFeature<double>("quality_score"),
                    InnovationScore = evaluation.GetFeature<double>("innovation_score"),
                    EfficiencyScore = evaluation.GetFeature<double>("efficiency_score"),
                    AccuracyScore = evaluation.GetFeature<double>("accuracy_score"),
                    OverallIntelligenceScore = evaluation.GetFeature<double>("intelligence_score")
                };
            });

            return (await Task.WhenAll(evaluationTasks)).ToList();
        }

        /// <summary>
        /// Calculates composite scores combining intelligence, stake, and performance
        /// </summary>
        private async Task<List<SelectedValidator>> CalculateCompositeScores(List<EvaluatedResponse> evaluatedResponses)
        {
            var scoredValidators = new List<SelectedValidator>();

            foreach (var response in evaluatedResponses)
            {
                var validator = response.ValidatorResponse.Validator;
                var validatorMetrics = await _validatorService.GetValidatorMetrics(validator.Id);

                // Weighted scoring: 40% intelligence, 35% stake, 25% performance history
                var intelligenceWeight = 0.40;
                var stakeWeight = 0.35;
                var performanceWeight = 0.25;

                var normalizedStake = NormalizeStake(validator.StakeAmount);
                var performanceScore = CalculatePerformanceScore(validatorMetrics);

                var compositeScore = 
                    (response.OverallIntelligenceScore * intelligenceWeight) +
                    (normalizedStake * stakeWeight) +
                    (performanceScore * performanceWeight);

                scoredValidators.Add(new SelectedValidator
                {
                    Validator = validator,
                    IntelligenceScore = response.OverallIntelligenceScore,
                    StakeScore = normalizedStake,
                    PerformanceScore = performanceScore,
                    CompositeScore = compositeScore,
                    SolutionQuality = response.QualityScore
                });
            }

            return scoredValidators;
        }

        /// <summary>
        /// Finalizes consensus with selected intelligent validators
        /// </summary>
        private async Task<ConsensusResult> FinalizeConsensus(List<SelectedValidator> selectedValidators, Block block)
        {
            if (selectedValidators.Count == 0)
            {
                return new ConsensusResult
                {
                    IsValid = false,
                    ErrorMessage = "No validators selected for consensus"
                };
            }

            // Perform Byzantine Fault Tolerant consensus with selected validators
            var consensusVotes = await CollectConsensusVotes(selectedValidators, block);
            
            // Require 2/3+ majority for consensus
            var requiredVotes = (selectedValidators.Count * 2) / 3 + 1;
            var positiveVotes = consensusVotes.Count(v => v.IsApproval);

            var isConsensusReached = positiveVotes >= requiredVotes;

            return new ConsensusResult
            {
                IsValid = isConsensusReached,
                ParticipatingValidators = selectedValidators.Select(v => v.Validator.Id).ToList(),
                VoteCount = consensusVotes.Count,
                ApprovalCount = positiveVotes,
                RequiredApprovals = requiredVotes,
                ConsensusStrength = (double)positiveVotes / selectedValidators.Count,
                IntelligenceMetrics = new IntelligenceMetrics
                {
                    AverageIntelligenceScore = selectedValidators.Average(v => v.IntelligenceScore),
                    TopIntelligenceScore = selectedValidators.Max(v => v.IntelligenceScore),
                    IntelligenceDistribution = CalculateIntelligenceDistribution(selectedValidators)
                }
            };
        }

        /// <summary>
        /// Updates validator intelligence scores based on performance
        /// </summary>
        private async Task UpdateValidatorIntelligenceScores(
            List<ValidatorResponse> responses, ConsensusResult consensusResult)
        {
            foreach (var response in responses)
            {
                var performanceUpdate = new ValidatorPerformanceUpdate
                {
                    ValidatorId = response.Validator.Id,
                    ParticipatedInConsensus = consensusResult.ParticipatingValidators.Contains(response.Validator.Id),
                    SolutionSubmitted = response.IsValid,
                    ConsensusSuccess = consensusResult.IsValid,
                    Timestamp = DateTime.UtcNow
                };

                await _validatorService.UpdateValidatorPerformance(performanceUpdate);
            }
        }

        /// <summary>
        /// Fallback to traditional stake-based consensus if PoI fails
        /// </summary>
        private async Task<ConsensusResult> FallbackConsensus(Block block)
        {
            _logger.LogWarning("Falling back to traditional consensus for block {BlockNumber}", block.Number);
            
            var allValidators = await _validatorService.GetEligibleValidators();
            var selectedByStake = await SelectByStakeOnly(allValidators);
            
            return await FinalizeConsensus(selectedByStake, block);
        }

        // Helper methods
        private double CalculateBlockComplexity(Block block)
        {
            var transactionComplexity = block.Transactions.Sum(tx => 
                (tx.Data?.Length ?? 0) + (tx.Value > 0 ? 10 : 0));
            
            return Math.Log10(transactionComplexity + 1);
        }

        private ChallengeType DetermineChallengeType(ChallengeContext context)
        {
            var random = new Random((int)(context.BlockNumber % int.MaxValue));
            
            return context.BlockComplexity switch
            {
                > 5.0 => ChallengeType.OptimizationChallenge,
                > 3.0 => ChallengeType.SecurityAnalysis,
                > 1.0 => ChallengeType.PatternRecognition,
                _ => ChallengeType.PredictiveModeling
            };
        }

        private double NormalizeStake(decimal stakeAmount)
        {
            // Normalize stake to 0-1 range using logarithmic scaling
            return Math.Log10((double)stakeAmount + 1) / Math.Log10(1000000 + 1);
        }

        private double CalculatePerformanceScore(ValidatorMetrics metrics)
        {
            return (metrics.UptimePercentage * 0.4) + 
                   (metrics.ConsensusParticipation * 0.3) + 
                   (metrics.BlockProposalSuccess * 0.3);
        }

        private int CalculateRequiredValidatorCount(Block block)
        {
            // Scale validator count based on network size and block importance
            var baseCount = 21; // Minimum validators
            var complexityMultiplier = Math.Min(2.0, CalculateBlockComplexity(block) / 5.0);
            
            return (int)(baseCount * (1 + complexityMultiplier));
        }

        private async Task<List<SelectedValidator>> SelectByStakeOnly(List<Validator> validators)
        {
            return validators
                .OrderByDescending(v => v.StakeAmount)
                .Take(21) // Default validator count
                .Select(v => new SelectedValidator
                {
                    Validator = v,
                    StakeScore = NormalizeStake(v.StakeAmount),
                    CompositeScore = NormalizeStake(v.StakeAmount)
                })
                .ToList();
        }

        private async Task<List<ConsensusVote>> CollectConsensusVotes(List<SelectedValidator> validators, Block block)
        {
            var voteTasks = validators.Select(async v =>
            {
                var vote = await _validatorService.RequestBlockVote(v.Validator.Id, block);
                return new ConsensusVote
                {
                    ValidatorId = v.Validator.Id,
                    IsApproval = vote,
                    Timestamp = DateTime.UtcNow
                };
            });

            return (await Task.WhenAll(voteTasks)).ToList();
        }

        private Dictionary<string, double> CalculateIntelligenceDistribution(List<SelectedValidator> validators)
        {
            var scores = validators.Select(v => v.IntelligenceScore).ToList();
            
            return new Dictionary<string, double>
            {
                ["min"] = scores.Min(),
                ["max"] = scores.Max(),
                ["mean"] = scores.Average(),
                ["median"] = scores.OrderBy(s => s).Skip(scores.Count / 2).First(),
                ["std_dev"] = CalculateStandardDeviation(scores)
            };
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            var mean = values.Average();
            var variance = values.Select(v => Math.Pow(v - mean, 2)).Average();
            return Math.Sqrt(variance);
        }

        private async Task<NetworkState> GetNetworkState()
        {
            return await _validatorService.GetCurrentNetworkState();
        }

        private async Task<ChallengeSolution> RequestValidatorSolution(
            Validator validator, AIChallenge challenge, TimeSpan timeout)
        {
            return await _validatorService.RequestChallengeSolution(validator.Id, challenge, timeout);
        }
    }

    // Supporting interfaces and models would be defined in separate files
    public interface IValidatorService
    {
        Task<List<Validator>> GetEligibleValidators();
        Task<ValidatorMetrics> GetValidatorMetrics(string validatorId);
        Task<ValidatorHistory> GetValidatorHistory(string validatorId);
        Task UpdateValidatorPerformance(ValidatorPerformanceUpdate update);
        Task<bool> RequestBlockVote(string validatorId, Block block);
        Task<ChallengeSolution> RequestChallengeSolution(string validatorId, AIChallenge challenge, TimeSpan timeout);
        Task<NetworkState> GetCurrentNetworkState();
    }

    public interface IChallengeGenerator
    {
        Task<AIChallenge> GenerateOptimizationChallenge(ChallengeContext context);
        Task<AIChallenge> GenerateSecurityChallenge(ChallengeContext context);
        Task<AIChallenge> GeneratePatternChallenge(ChallengeContext context);
        Task<AIChallenge> GeneratePredictionChallenge(ChallengeContext context);
        Task<AIChallenge> GenerateGeneralChallenge(ChallengeContext context);
    }
}
