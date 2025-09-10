using LksBrothers.Core.Primitives;
using LksBrothers.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LksBrothers.StateManagement.Services;

public class StateSnapshot : IStateSnapshot
{
    private readonly ILogger<StateSnapshot> _logger;
    private readonly StateService _baseStateService;
    private readonly ConcurrentDictionary<Address, AccountState> _modifiedAccounts;
    private readonly ConcurrentDictionary<Address, UInt256> _balanceChanges;
    private readonly ConcurrentDictionary<Address, ulong> _nonceChanges;
    private readonly object _lockObject = new();

    public StateSnapshot(ILogger<StateSnapshot> logger, StateService baseStateService)
    {
        _logger = logger;
        _baseStateService = baseStateService;
        _modifiedAccounts = new ConcurrentDictionary<Address, AccountState>();
        _balanceChanges = new ConcurrentDictionary<Address, UInt256>();
        _nonceChanges = new ConcurrentDictionary<Address, ulong>();
    }

    public async Task<Hash> ComputeRootHashAsync()
    {
        try
        {
            // Get all modified account states
            var allAccounts = new List<AccountState>();
            
            // Add modified accounts
            foreach (var kvp in _modifiedAccounts)
            {
                allAccounts.Add(kvp.Value);
            }

            // Add accounts with balance/nonce changes that aren't already in modified accounts
            foreach (var address in _balanceChanges.Keys.Union(_nonceChanges.Keys))
            {
                if (!_modifiedAccounts.ContainsKey(address))
                {
                    var baseAccount = await _baseStateService.GetAccountStateAsync(address);
                    if (baseAccount != null)
                    {
                        var modifiedAccount = new AccountState
                        {
                            Address = address,
                            Balance = _balanceChanges.GetValueOrDefault(address, baseAccount.Balance),
                            Nonce = _nonceChanges.GetValueOrDefault(address, baseAccount.Nonce),
                            CodeHash = baseAccount.CodeHash,
                            StorageRoot = baseAccount.StorageRoot
                        };
                        allAccounts.Add(modifiedAccount);
                    }
                }
            }

            // Sort accounts by address for deterministic root computation
            allAccounts.Sort((a, b) => string.Compare(a.Address.ToString(), b.Address.ToString(), StringComparison.Ordinal));

            // Compute Merkle root of all account states
            return ComputeAccountsMerkleRoot(allAccounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing state root hash");
            throw;
        }
    }

    public async Task SubtractBalanceAsync(Address address, UInt256 amount)
    {
        try
        {
            var currentBalance = await GetBalanceAsync(address);
            if (currentBalance < amount)
            {
                throw new InvalidOperationException($"Insufficient balance for address {address}. Current: {currentBalance}, Required: {amount}");
            }

            var newBalance = currentBalance - amount;
            _balanceChanges.AddOrUpdate(address, newBalance, (key, oldValue) => newBalance);

            _logger.LogDebug("Subtracted {Amount} from {Address}, new balance: {NewBalance}", amount, address, newBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subtracting balance from {Address}", address);
            throw;
        }
    }

    public async Task AddBalanceAsync(Address address, UInt256 amount)
    {
        try
        {
            var currentBalance = await GetBalanceAsync(address);
            var newBalance = currentBalance + amount;
            
            _balanceChanges.AddOrUpdate(address, newBalance, (key, oldValue) => newBalance);

            _logger.LogDebug("Added {Amount} to {Address}, new balance: {NewBalance}", amount, address, newBalance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding balance to {Address}", address);
            throw;
        }
    }

    public async Task IncrementNonceAsync(Address address)
    {
        try
        {
            var currentNonce = await GetNonceAsync(address);
            var newNonce = currentNonce + 1;
            
            _nonceChanges.AddOrUpdate(address, newNonce, (key, oldValue) => newNonce);

            _logger.LogDebug("Incremented nonce for {Address} to {NewNonce}", address, newNonce);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error incrementing nonce for {Address}", address);
            throw;
        }
    }

    public async Task<UInt256> GetBalanceAsync(Address address)
    {
        // Check if we have a modified balance first
        if (_balanceChanges.TryGetValue(address, out var modifiedBalance))
        {
            return modifiedBalance;
        }

        // Check if we have a modified account
        if (_modifiedAccounts.TryGetValue(address, out var modifiedAccount))
        {
            return modifiedAccount.Balance;
        }

        // Fall back to base state service
        return await _baseStateService.GetBalanceAsync(address);
    }

    public async Task<ulong> GetNonceAsync(Address address)
    {
        // Check if we have a modified nonce first
        if (_nonceChanges.TryGetValue(address, out var modifiedNonce))
        {
            return modifiedNonce;
        }

        // Check if we have a modified account
        if (_modifiedAccounts.TryGetValue(address, out var modifiedAccount))
        {
            return modifiedAccount.Nonce;
        }

        // Fall back to base state service
        return await _baseStateService.GetNonceAsync(address);
    }

    public async Task SetAccountStateAsync(Address address, AccountState accountState)
    {
        try
        {
            _modifiedAccounts.AddOrUpdate(address, accountState, (key, oldValue) => accountState);
            
            _logger.LogDebug("Set account state for {Address}", address);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting account state for {Address}", address);
            throw;
        }
    }

    public async Task<AccountState?> GetAccountStateAsync(Address address)
    {
        // Check modified accounts first
        if (_modifiedAccounts.TryGetValue(address, out var modifiedAccount))
        {
            return modifiedAccount;
        }

        // Get base account and apply any balance/nonce changes
        var baseAccount = await _baseStateService.GetAccountStateAsync(address);
        if (baseAccount == null)
        {
            // Create new account if it doesn't exist and we have changes
            if (_balanceChanges.ContainsKey(address) || _nonceChanges.ContainsKey(address))
            {
                return new AccountState
                {
                    Address = address,
                    Balance = _balanceChanges.GetValueOrDefault(address, UInt256.Zero),
                    Nonce = _nonceChanges.GetValueOrDefault(address, 0),
                    CodeHash = Hash.Zero,
                    StorageRoot = Hash.Zero
                };
            }
            return null;
        }

        // Apply any pending changes
        var balance = _balanceChanges.GetValueOrDefault(address, baseAccount.Balance);
        var nonce = _nonceChanges.GetValueOrDefault(address, baseAccount.Nonce);

        if (balance != baseAccount.Balance || nonce != baseAccount.Nonce)
        {
            return new AccountState
            {
                Address = address,
                Balance = balance,
                Nonce = nonce,
                CodeHash = baseAccount.CodeHash,
                StorageRoot = baseAccount.StorageRoot
            };
        }

        return baseAccount;
    }

    public Dictionary<Address, AccountState> GetModifiedAccounts()
    {
        var result = new Dictionary<Address, AccountState>();

        // Add explicitly modified accounts
        foreach (var kvp in _modifiedAccounts)
        {
            result[kvp.Key] = kvp.Value;
        }

        // Add accounts with balance/nonce changes
        foreach (var address in _balanceChanges.Keys.Union(_nonceChanges.Keys))
        {
            if (!result.ContainsKey(address))
            {
                var balance = _balanceChanges.GetValueOrDefault(address, UInt256.Zero);
                var nonce = _nonceChanges.GetValueOrDefault(address, 0);
                
                result[address] = new AccountState
                {
                    Address = address,
                    Balance = balance,
                    Nonce = nonce,
                    CodeHash = Hash.Zero,
                    StorageRoot = Hash.Zero
                };
            }
        }

        return result;
    }

    public void Clear()
    {
        lock (_lockObject)
        {
            _modifiedAccounts.Clear();
            _balanceChanges.Clear();
            _nonceChanges.Clear();
        }
    }

    private Hash ComputeAccountsMerkleRoot(List<AccountState> accounts)
    {
        if (!accounts.Any())
        {
            return Hash.Zero;
        }

        // Create leaf hashes for each account
        var leafHashes = accounts.Select(account => ComputeAccountHash(account)).ToList();
        
        // Build Merkle tree
        return BuildMerkleRoot(leafHashes);
    }

    private Hash ComputeAccountHash(AccountState account)
    {
        var data = new List<byte>();
        data.AddRange(account.Address.ToByteArray());
        data.AddRange(account.Balance.ToByteArray());
        data.AddRange(BitConverter.GetBytes(account.Nonce));
        data.AddRange(account.CodeHash.ToByteArray());
        data.AddRange(account.StorageRoot.ToByteArray());
        
        return Hash.ComputeHash(data.ToArray());
    }

    private Hash BuildMerkleRoot(List<Hash> hashes)
    {
        if (hashes.Count == 1)
        {
            return hashes[0];
        }

        var nextLevel = new List<Hash>();
        for (int i = 0; i < hashes.Count; i += 2)
        {
            var left = hashes[i];
            var right = i + 1 < hashes.Count ? hashes[i + 1] : left;
            
            var combined = new byte[64];
            left.ToByteArray().CopyTo(combined, 0);
            right.ToByteArray().CopyTo(combined, 32);
            
            nextLevel.Add(Hash.ComputeHash(combined));
        }

        return BuildMerkleRoot(nextLevel);
    }
}

public class AccountState
{
    public required Address Address { get; set; }
    public required UInt256 Balance { get; set; }
    public required ulong Nonce { get; set; }
    public required Hash CodeHash { get; set; }
    public required Hash StorageRoot { get; set; }
}

// Update the interface to match the implementation
public interface IStateSnapshot
{
    Task<Hash> ComputeRootHashAsync();
    Task SubtractBalanceAsync(Address address, UInt256 amount);
    Task AddBalanceAsync(Address address, UInt256 amount);
    Task IncrementNonceAsync(Address address);
    Task<UInt256> GetBalanceAsync(Address address);
    Task<ulong> GetNonceAsync(Address address);
    Task SetAccountStateAsync(Address address, AccountState accountState);
    Task<AccountState?> GetAccountStateAsync(Address address);
    Dictionary<Address, AccountState> GetModifiedAccounts();
    void Clear();
}
