using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using Microsoft.Extensions.Logging;
using Nethereum.EVM;
using Nethereum.Util;
using System.Collections.Concurrent;

namespace LksBrothers.Execution.Engine;

/// <summary>
/// EVM-compatible execution engine for smart contracts and transactions
/// </summary>
public class EvmExecutionEngine : IExecutionEngine
{
    private readonly ILogger<EvmExecutionEngine> _logger;
    private readonly IStateManager _stateManager;
    private readonly IGasCalculator _gasCalculator;
    private readonly IPrecompileManager _precompileManager;
    
    private readonly ConcurrentDictionary<Address, ContractCode> _contractCache = new();
    
    public EvmExecutionEngine(
        ILogger<EvmExecutionEngine> logger,
        IStateManager stateManager,
        IGasCalculator gasCalculator,
        IPrecompileManager precompileManager)
    {
        _logger = logger;
        _stateManager = stateManager;
        _gasCalculator = gasCalculator;
        _precompileManager = precompileManager;
    }
    
    public async Task<ExecutionResult> ExecuteTransactionAsync(
        Transaction transaction, 
        ExecutionContext context)
    {
        try
        {
            _logger.LogDebug("Executing transaction {Hash}", transaction.Hash);
            
            // Validate transaction
            var validation = transaction.Validate();
            if (!validation.IsValid)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = string.Join(", ", validation.Errors),
                    GasUsed = UInt256.Zero
                };
            }
            
            // Check account balance and nonce
            var fromAccount = await _stateManager.GetAccountAsync(transaction.From);
            if (fromAccount == null)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Account not found",
                    GasUsed = UInt256.Zero
                };
            }
            
            if (fromAccount.Nonce != transaction.Nonce)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Invalid nonce",
                    GasUsed = UInt256.Zero
                };
            }
            
            // Calculate gas cost
            var intrinsicGas = _gasCalculator.CalculateIntrinsicGas(transaction);
            if (transaction.GasLimit < intrinsicGas)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Intrinsic gas too low",
                    GasUsed = UInt256.Zero
                };
            }
            
            // Check balance for value + gas
            var totalCost = transaction.Value + (transaction.GasLimit * transaction.GasPrice);
            if (fromAccount.Balance < totalCost)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Insufficient balance",
                    GasUsed = UInt256.Zero
                };
            }
            
            // Create execution environment
            var environment = new ExecutionEnvironment
            {
                Transaction = transaction,
                Context = context,
                GasRemaining = transaction.GasLimit - intrinsicGas,
                StateManager = _stateManager
            };
            
            // Execute based on transaction type
            ExecutionResult result;
            
            if (transaction.To == Address.Zero)
            {
                // Contract deployment
                result = await ExecuteContractDeployment(environment);
            }
            else if (await _stateManager.IsContractAsync(transaction.To))
            {
                // Contract call
                result = await ExecuteContractCall(environment);
            }
            else if (_precompileManager.IsPrecompile(transaction.To))
            {
                // Precompile call
                result = await ExecutePrecompile(environment);
            }
            else
            {
                // Simple transfer
                result = await ExecuteTransfer(environment);
            }
            
            // Apply state changes if successful
            if (result.Success)
            {
                await ApplyStateChanges(transaction, result, environment);
            }
            
            // Always charge gas (even on failure)
            await ChargeGas(transaction, result.GasUsed);
            
            _logger.LogDebug("Transaction {Hash} executed: Success={Success}, GasUsed={GasUsed}",
                transaction.Hash, result.Success, result.GasUsed);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing transaction {Hash}", transaction.Hash);
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                GasUsed = transaction.GasLimit // Charge all gas on error
            };
        }
    }
    
    private async Task<ExecutionResult> ExecuteContractDeployment(ExecutionEnvironment environment)
    {
        var transaction = environment.Transaction;
        
        // Calculate contract address
        var contractAddress = Address.CreateContractAddress(transaction.From, transaction.Nonce);
        
        // Check if address is already occupied
        if (await _stateManager.AccountExistsAsync(contractAddress))
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = "Contract address already exists",
                GasUsed = environment.GasRemaining
            };
        }
        
        // Create contract account
        var contractAccount = new Account
        {
            Address = contractAddress,
            Balance = transaction.Value,
            Nonce = 1,
            CodeHash = Hash.Compute(transaction.Data)
        };
        
        // Execute constructor
        var vm = new EvmVirtualMachine(environment);
        var constructorResult = await vm.ExecuteAsync(transaction.Data, contractAddress);
        
        if (!constructorResult.Success)
        {
            return constructorResult;
        }
        
        // Store contract code
        if (constructorResult.ReturnData.Length > 0)
        {
            var codeStorageCost = _gasCalculator.CalculateCodeStorageCost(constructorResult.ReturnData);
            if (environment.GasRemaining < codeStorageCost)
            {
                return new ExecutionResult
                {
                    Success = false,
                    ErrorMessage = "Out of gas for code storage",
                    GasUsed = environment.GasRemaining
                };
            }
            
            await _stateManager.SetCodeAsync(contractAddress, constructorResult.ReturnData);
            environment.GasRemaining -= codeStorageCost;
        }
        
        return new ExecutionResult
        {
            Success = true,
            ContractAddress = contractAddress,
            ReturnData = constructorResult.ReturnData,
            GasUsed = transaction.GasLimit - environment.GasRemaining,
            Logs = constructorResult.Logs
        };
    }
    
    private async Task<ExecutionResult> ExecuteContractCall(ExecutionEnvironment environment)
    {
        var transaction = environment.Transaction;
        
        // Get contract code
        var code = await _stateManager.GetCodeAsync(transaction.To);
        if (code.Length == 0)
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = "Contract has no code",
                GasUsed = UInt256.Zero
            };
        }
        
        // Execute contract
        var vm = new EvmVirtualMachine(environment);
        var result = await vm.ExecuteAsync(code, transaction.To, transaction.Data);
        
        return new ExecutionResult
        {
            Success = result.Success,
            ReturnData = result.ReturnData,
            GasUsed = transaction.GasLimit - environment.GasRemaining,
            Logs = result.Logs,
            ErrorMessage = result.ErrorMessage
        };
    }
    
    private async Task<ExecutionResult> ExecutePrecompile(ExecutionEnvironment environment)
    {
        var transaction = environment.Transaction;
        
        var precompile = _precompileManager.GetPrecompile(transaction.To);
        if (precompile == null)
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = "Precompile not found",
                GasUsed = UInt256.Zero
            };
        }
        
        var gasCost = precompile.CalculateGasCost(transaction.Data);
        if (environment.GasRemaining < gasCost)
        {
            return new ExecutionResult
            {
                Success = false,
                ErrorMessage = "Out of gas for precompile",
                GasUsed = environment.GasRemaining
            };
        }
        
        var result = await precompile.ExecuteAsync(transaction.Data);
        
        return new ExecutionResult
        {
            Success = result.Success,
            ReturnData = result.Output,
            GasUsed = gasCost,
            ErrorMessage = result.ErrorMessage
        };
    }
    
    private async Task<ExecutionResult> ExecuteTransfer(ExecutionEnvironment environment)
    {
        var transaction = environment.Transaction;
        
        // Simple value transfer
        if (transaction.Value > UInt256.Zero)
        {
            var toAccount = await _stateManager.GetAccountAsync(transaction.To) ?? 
                           new Account { Address = transaction.To };
            
            toAccount.Balance += transaction.Value;
            await _stateManager.SetAccountAsync(toAccount);
        }
        
        return new ExecutionResult
        {
            Success = true,
            ReturnData = Array.Empty<byte>(),
            GasUsed = _gasCalculator.CalculateIntrinsicGas(transaction),
            Logs = new List<Log>()
        };
    }
    
    private async Task ApplyStateChanges(
        Transaction transaction, 
        ExecutionResult result, 
        ExecutionEnvironment environment)
    {
        // Update sender account
        var fromAccount = await _stateManager.GetAccountAsync(transaction.From);
        if (fromAccount != null)
        {
            fromAccount.Nonce++;
            fromAccount.Balance -= transaction.Value;
            await _stateManager.SetAccountAsync(fromAccount);
        }
        
        // Apply any state changes from contract execution
        await environment.StateManager.CommitChangesAsync();
    }
    
    private async Task ChargeGas(Transaction transaction, UInt256 gasUsed)
    {
        var fromAccount = await _stateManager.GetAccountAsync(transaction.From);
        if (fromAccount != null)
        {
            var gasCost = gasUsed * transaction.GasPrice;
            fromAccount.Balance -= gasCost;
            await _stateManager.SetAccountAsync(fromAccount);
        }
    }
}

public class ExecutionEnvironment
{
    public Transaction Transaction { get; set; } = new();
    public ExecutionContext Context { get; set; } = new();
    public UInt256 GasRemaining { get; set; } = UInt256.Zero;
    public IStateManager StateManager { get; set; } = null!;
}

public class ExecutionContext
{
    public Hash BlockHash { get; set; } = Hash.Zero;
    public ulong BlockNumber { get; set; }
    public DateTime BlockTimestamp { get; set; } = DateTime.UtcNow;
    public Address Coinbase { get; set; } = Address.Zero;
    public UInt256 Difficulty { get; set; } = UInt256.Zero;
    public UInt256 GasLimit { get; set; } = UInt256.Zero;
    public UInt256 BaseFee { get; set; } = UInt256.Zero;
    public Hash PrevRandao { get; set; } = Hash.Zero;
}

public class ExecutionResult
{
    public bool Success { get; set; }
    public byte[] ReturnData { get; set; } = Array.Empty<byte>();
    public UInt256 GasUsed { get; set; } = UInt256.Zero;
    public List<Log> Logs { get; set; } = new();
    public Address? ContractAddress { get; set; }
    public string? ErrorMessage { get; set; }
}

public class Account
{
    public Address Address { get; set; } = Address.Zero;
    public UInt256 Balance { get; set; } = UInt256.Zero;
    public ulong Nonce { get; set; }
    public Hash CodeHash { get; set; } = Hash.Zero;
    public Hash StorageRoot { get; set; } = Hash.Zero;
}

public class ContractCode
{
    public byte[] Code { get; set; } = Array.Empty<byte>();
    public Hash Hash { get; set; } = Hash.Zero;
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

// Interfaces
public interface IExecutionEngine
{
    Task<ExecutionResult> ExecuteTransactionAsync(Transaction transaction, ExecutionContext context);
}

public interface IStateManager
{
    Task<Account?> GetAccountAsync(Address address);
    Task SetAccountAsync(Account account);
    Task<bool> AccountExistsAsync(Address address);
    Task<bool> IsContractAsync(Address address);
    Task<byte[]> GetCodeAsync(Address address);
    Task SetCodeAsync(Address address, byte[] code);
    Task<byte[]> GetStorageAsync(Address address, UInt256 key);
    Task SetStorageAsync(Address address, UInt256 key, byte[] value);
    Task CommitChangesAsync();
    Task RevertChangesAsync();
}

public interface IGasCalculator
{
    UInt256 CalculateIntrinsicGas(Transaction transaction);
    UInt256 CalculateCodeStorageCost(byte[] code);
    UInt256 CalculateStorageCost(UInt256 currentValue, UInt256 newValue);
}

public interface IPrecompileManager
{
    bool IsPrecompile(Address address);
    IPrecompile? GetPrecompile(Address address);
}

public interface IPrecompile
{
    UInt256 CalculateGasCost(byte[] input);
    Task<PrecompileResult> ExecuteAsync(byte[] input);
}

public class PrecompileResult
{
    public bool Success { get; set; }
    public byte[] Output { get; set; } = Array.Empty<byte>();
    public string? ErrorMessage { get; set; }
}
