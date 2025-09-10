using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasmtime;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using System.Text.Json;

namespace LksBrothers.Hooks.Engine;

public class HookExecutor : IDisposable
{
    private readonly ILogger<HookExecutor> _logger;
    private readonly HookExecutorOptions _options;
    private readonly Wasmtime.Engine _wasmEngine;
    private readonly Dictionary<string, CompiledHook> _compiledHooks;
    private readonly object _executionLock = new();

    public HookExecutor(ILogger<HookExecutor> logger, IOptions<HookExecutorOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _wasmEngine = new Wasmtime.Engine();
        _compiledHooks = new Dictionary<string, CompiledHook>();
        
        LoadHooks();
    }

    public async Task<HookExecutionResult> ExecuteZeroFeeHookAsync(LksCoinTransaction transaction)
    {
        try
        {
            if (!_compiledHooks.TryGetValue("zero_fee_hook", out var hook))
            {
                return HookExecutionResult.Failed("Zero fee hook not found");
            }

            var context = new HookExecutionContext
            {
                Transaction = transaction,
                BlockTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FoundationAccount = _options.FoundationAccount,
                MaxGas = _options.MaxGasPerExecution
            };

            return await ExecuteHookAsync(hook, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing zero fee hook for transaction {TxHash}", transaction.Hash);
            return HookExecutionResult.Failed($"Hook execution error: {ex.Message}");
        }
    }

    public async Task<HookExecutionResult> ExecuteGovernanceHookAsync(GovernanceProposal proposal)
    {
        try
        {
            if (!_compiledHooks.TryGetValue("governance_hook", out var hook))
            {
                return HookExecutionResult.Failed("Governance hook not found");
            }

            var context = new HookExecutionContext
            {
                Proposal = proposal,
                BlockTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                MaxGas = _options.MaxGasPerExecution
            };

            return await ExecuteHookAsync(hook, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing governance hook for proposal {ProposalId}", proposal.Id);
            return HookExecutionResult.Failed($"Hook execution error: {ex.Message}");
        }
    }

    public async Task<HookExecutionResult> ExecuteCrossChainHookAsync(CrossChainMessage message)
    {
        try
        {
            if (!_compiledHooks.TryGetValue("cross_chain_hook", out var hook))
            {
                return HookExecutionResult.Failed("Cross-chain hook not found");
            }

            var context = new HookExecutionContext
            {
                CrossChainMessage = message,
                BlockTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                MaxGas = _options.MaxGasPerExecution
            };

            return await ExecuteHookAsync(hook, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing cross-chain hook for message {MessageId}", message.Id);
            return HookExecutionResult.Failed($"Hook execution error: {ex.Message}");
        }
    }

    private async Task<HookExecutionResult> ExecuteHookAsync(CompiledHook hook, HookExecutionContext context)
    {
        lock (_executionLock)
        {
            try
            {
                using var store = new Store(_wasmEngine);
                var instance = new Instance(store, hook.Module, Array.Empty<object>());
                
                // Set up WASM memory and imports
                SetupHookEnvironment(store, instance, context);
                
                // Get the main hook function
                var hookFunction = instance.GetFunction(store, "hook");
                if (hookFunction == null)
                {
                    return HookExecutionResult.Failed("Hook function 'hook' not found");
                }

                // Execute the hook with gas metering
                var gasUsed = 0UL;
                var startTime = DateTimeOffset.UtcNow;
                
                var result = hookFunction.Invoke(store);
                
                var executionTime = DateTimeOffset.UtcNow - startTime;
                gasUsed = (ulong)executionTime.TotalMicroseconds; // Simple gas calculation
                
                if (gasUsed > context.MaxGas)
                {
                    return HookExecutionResult.Failed("Hook execution exceeded gas limit");
                }

                var returnCode = result as int? ?? -1;
                
                return returnCode switch
                {
                    0 => HookExecutionResult.Success("Hook executed successfully", gasUsed),
                    1 => HookExecutionResult.Rejected("Hook rejected transaction", gasUsed),
                    _ => HookExecutionResult.Failed($"Hook returned error code: {returnCode}", gasUsed)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WASM execution error in hook {HookName}", hook.Name);
                return HookExecutionResult.Failed($"WASM execution error: {ex.Message}");
            }
        }
    }

    private void SetupHookEnvironment(Store store, Instance instance, HookExecutionContext context)
    {
        // Set up memory and host functions for the WASM module
        // This would include functions like:
        // - otxn_type() - get transaction type
        // - otxn_slot() - get transaction data
        // - slot_set() - set transaction data
        // - accept() - accept transaction
        // - reject() - reject transaction
        // - trace_u64() - logging function

        // For now, we'll set up basic context data in memory
        var memory = instance.GetMemory(store, "memory");
        if (memory != null && context.Transaction != null)
        {
            var txData = JsonSerializer.SerializeToUtf8Bytes(context.Transaction);
            var span = memory.GetSpan(store);
            if (txData.Length <= span.Length)
            {
                txData.CopyTo(span);
            }
        }
    }

    private void LoadHooks()
    {
        try
        {
            var hooksDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wasm");
            if (!Directory.Exists(hooksDirectory))
            {
                _logger.LogWarning("Hooks directory not found: {Directory}", hooksDirectory);
                return;
            }

            var wasmFiles = Directory.GetFiles(hooksDirectory, "*.wasm");
            foreach (var wasmFile in wasmFiles)
            {
                try
                {
                    var hookName = Path.GetFileNameWithoutExtension(wasmFile);
                    var wasmBytes = File.ReadAllBytes(wasmFile);
                    var module = Module.FromBytes(_wasmEngine, wasmBytes);
                    
                    _compiledHooks[hookName] = new CompiledHook
                    {
                        Name = hookName,
                        Module = module,
                        LoadedAt = DateTimeOffset.UtcNow
                    };
                    
                    _logger.LogInformation("Loaded hook: {HookName} from {FilePath}", hookName, wasmFile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load hook from {FilePath}", wasmFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading hooks");
        }
    }

    public void Dispose()
    {
        foreach (var hook in _compiledHooks.Values)
        {
            hook.Module?.Dispose();
        }
        _compiledHooks.Clear();
        _wasmEngine?.Dispose();
    }
}

public class CompiledHook
{
    public required string Name { get; set; }
    public required Module Module { get; set; }
    public required DateTimeOffset LoadedAt { get; set; }
}

public class HookExecutionContext
{
    public LksCoinTransaction? Transaction { get; set; }
    public GovernanceProposal? Proposal { get; set; }
    public CrossChainMessage? CrossChainMessage { get; set; }
    public ulong BlockTimestamp { get; set; }
    public Address? FoundationAccount { get; set; }
    public ulong MaxGas { get; set; } = 1_000_000;
}

public class HookExecutionResult
{
    public required bool Success { get; set; }
    public required string Message { get; set; }
    public ulong GasUsed { get; set; }
    public Dictionary<string, object>? Data { get; set; }

    public static HookExecutionResult Success(string message, ulong gasUsed = 0)
    {
        return new HookExecutionResult
        {
            Success = true,
            Message = message,
            GasUsed = gasUsed
        };
    }

    public static HookExecutionResult Failed(string message, ulong gasUsed = 0)
    {
        return new HookExecutionResult
        {
            Success = false,
            Message = message,
            GasUsed = gasUsed
        };
    }

    public static HookExecutionResult Rejected(string message, ulong gasUsed = 0)
    {
        return new HookExecutionResult
        {
            Success = false,
            Message = message,
            GasUsed = gasUsed,
            Data = new Dictionary<string, object> { ["rejected"] = true }
        };
    }
}

public class GovernanceProposal
{
    public required Hash Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required Address Proposer { get; set; }
    public required ulong VotingStartTime { get; set; }
    public required ulong VotingEndTime { get; set; }
    public required Dictionary<string, object> Parameters { get; set; }
}

public class CrossChainMessage
{
    public required Hash Id { get; set; }
    public required string SourceChain { get; set; }
    public required string DestinationChain { get; set; }
    public required Address Sender { get; set; }
    public required Address Recipient { get; set; }
    public required byte[] Payload { get; set; }
    public required ulong Timestamp { get; set; }
}

public class HookExecutorOptions
{
    public Address? FoundationAccount { get; set; }
    public ulong MaxGasPerExecution { get; set; } = 1_000_000;
    public int MaxConcurrentExecutions { get; set; } = 10;
    public string HooksDirectory { get; set; } = "./wasm";
    public bool EnableGasMetering { get; set; } = true;
}
