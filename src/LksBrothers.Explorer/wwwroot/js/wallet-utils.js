// Professional wallet utility functions and helper methods
class WalletUtils {
    
    // Get wallet-specific styling and icons
    static getWalletIcon(walletType) {
        const icons = {
            'metamask': 'fab fa-ethereum',
            'phantom': 'fas fa-ghost',
            'xaman': 'fas fa-coins',
            'lks-wallet': 'fas fa-wallet'
        };
        return icons[walletType] || 'fas fa-wallet';
    }
    
    static getWalletColor(walletType) {
        const colors = {
            'metamask': 'bg-orange-500',
            'phantom': 'bg-purple-500',
            'xaman': 'bg-blue-500',
            'lks-wallet': 'bg-green-500'
        };
        return colors[walletType] || 'bg-gray-500';
    }
    
    static getWalletDisplayName(walletType) {
        const names = {
            'metamask': 'MetaMask',
            'phantom': 'Phantom',
            'xaman': 'Xaman',
            'lks-wallet': 'LKS Wallet'
        };
        return names[walletType] || 'Unknown Wallet';
    }
    
    static getNetworkName(walletType) {
        const networks = {
            'metamask': 'LKS Network',
            'phantom': 'Solana Mainnet',
            'xaman': 'XRPL Mainnet',
            'lks-wallet': 'LKS Network'
        };
        return networks[walletType] || 'Unknown Network';
    }
    
    // Format balance display
    static formatBalance(balance, walletType) {
        if (!balance) return '<div class="text-center py-4 text-gray-400">No balance data</div>';
        
        let balanceHtml = '<div class="bg-gray-900/50 p-4 rounded-lg">';
        balanceHtml += '<div class="text-xs text-gray-400 mb-2">Balance</div>';
        
        switch (walletType) {
            case 'metamask':
                balanceHtml += `
                    <div class="space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-green-400 font-semibold">LKS</span>
                            <span class="text-white font-mono">${(balance.lks || 0).toFixed(4)}</span>
                        </div>
                        <div class="flex justify-between items-center">
                            <span class="text-blue-400 font-semibold">ETH</span>
                            <span class="text-white font-mono">${(balance.eth || 0).toFixed(6)}</span>
                        </div>
                    </div>
                `;
                break;
                
            case 'phantom':
                balanceHtml += `
                    <div class="space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-purple-400 font-semibold">SOL</span>
                            <span class="text-white font-mono">${(balance.sol || 0).toFixed(4)}</span>
                        </div>
                        <div class="flex justify-between items-center">
                            <span class="text-green-400 font-semibold">LKS</span>
                            <span class="text-white font-mono">${(balance.lks || 0).toFixed(4)}</span>
                        </div>
                        ${balance.tokens ? `
                        <div class="flex justify-between items-center">
                            <span class="text-gray-400 text-sm">Tokens</span>
                            <span class="text-gray-300 text-sm">${balance.tokens}</span>
                        </div>
                        ` : ''}
                    </div>
                `;
                break;
                
            case 'xaman':
                balanceHtml += `
                    <div class="space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-blue-400 font-semibold">XRP</span>
                            <span class="text-white font-mono">${(balance.xrp || 0).toFixed(4)}</span>
                        </div>
                        <div class="flex justify-between items-center">
                            <span class="text-green-400 font-semibold">LKS</span>
                            <span class="text-white font-mono">${(balance.lks || 0).toFixed(4)}</span>
                        </div>
                        ${balance.tokens ? `
                        <div class="flex justify-between items-center">
                            <span class="text-gray-400 text-sm">Trustlines</span>
                            <span class="text-gray-300 text-sm">${balance.tokens}</span>
                        </div>
                        ` : ''}
                    </div>
                `;
                break;
                
            case 'lks-wallet':
                balanceHtml += `
                    <div class="space-y-2">
                        <div class="flex justify-between items-center">
                            <span class="text-green-400 font-semibold">LKS</span>
                            <span class="text-white font-mono text-lg">${(balance.lks || 0).toFixed(4)}</span>
                        </div>
                        <div class="text-xs text-green-300 mt-1">
                            <i class="fas fa-bolt mr-1"></i>Zero transaction fees
                        </div>
                    </div>
                `;
                break;
                
            default:
                balanceHtml += '<div class="text-gray-400">Balance not available</div>';
        }
        
        balanceHtml += '</div>';
        return balanceHtml;
    }
    
    // Address validation
    static validateAddress(address, walletType) {
        if (!address) return false;
        
        switch (walletType) {
            case 'metamask':
            case 'lks-wallet':
                // Ethereum-style addresses (0x...)
                if (address.startsWith('0x')) {
                    return /^0x[a-fA-F0-9]{40}$/.test(address);
                }
                // LKS native addresses (lks1...)
                if (address.startsWith('lks1')) {
                    return /^lks1[a-z0-9]{39,59}$/.test(address);
                }
                return false;
                
            case 'phantom':
                // Solana addresses (base58, 32-44 chars)
                return /^[1-9A-HJ-NP-Za-km-z]{32,44}$/.test(address);
                
            case 'xaman':
                // XRPL addresses (r...)
                return /^r[1-9A-HJ-NP-Za-km-z]{25,34}$/.test(address);
                
            default:
                return false;
        }
    }
    
    // Transaction status helpers
    static getTransactionStatusIcon(status) {
        const icons = {
            'pending': 'fas fa-clock text-yellow-400',
            'confirmed': 'fas fa-check-circle text-green-400',
            'failed': 'fas fa-times-circle text-red-400',
            'cancelled': 'fas fa-ban text-gray-400'
        };
        return icons[status] || 'fas fa-question-circle text-gray-400';
    }
    
    static getTransactionTypeLabel(type) {
        const labels = {
            'transfer': 'Transfer',
            'payment': 'Service Payment',
            'swap': 'Token Swap',
            'stake': 'Staking',
            'unstake': 'Unstaking',
            'bridge': 'Cross-chain Bridge'
        };
        return labels[type] || 'Transaction';
    }
    
    // Format transaction for display
    static formatTransaction(tx, userAddress) {
        const isOutgoing = tx.fromAddress?.toLowerCase() === userAddress?.toLowerCase();
        const direction = isOutgoing ? 'Sent' : 'Received';
        const directionIcon = isOutgoing ? 'fas fa-arrow-up text-red-400' : 'fas fa-arrow-down text-green-400';
        const otherAddress = isOutgoing ? tx.toAddress : tx.fromAddress;
        
        return `
            <div class="flex items-center justify-between p-3 bg-gray-800/50 rounded-lg hover:bg-gray-700/50 transition-colors">
                <div class="flex items-center space-x-3">
                    <div class="w-10 h-10 bg-gray-700 rounded-full flex items-center justify-center">
                        <i class="${directionIcon}"></i>
                    </div>
                    <div>
                        <div class="font-semibold text-white">${direction} ${this.getTransactionTypeLabel(tx.transactionType)}</div>
                        <div class="text-sm text-gray-400">
                            ${isOutgoing ? 'To:' : 'From:'} ${this.formatAddress(otherAddress)}
                        </div>
                        <div class="text-xs text-gray-500">
                            ${new Date(tx.timestamp).toLocaleDateString()} • 
                            <span class="${this.getTransactionStatusIcon(tx.status).includes('green') ? 'text-green-400' : 
                                           this.getTransactionStatusIcon(tx.status).includes('red') ? 'text-red-400' : 
                                           'text-yellow-400'}">${tx.status}</span>
                        </div>
                    </div>
                </div>
                <div class="text-right">
                    <div class="font-semibold ${isOutgoing ? 'text-red-400' : 'text-green-400'}">
                        ${isOutgoing ? '-' : '+'}${tx.amount} LKS
                    </div>
                    <div class="text-xs text-gray-400">
                        Fee: ${tx.gasFee || 0} LKS
                    </div>
                </div>
            </div>
        `;
    }
    
    // Format address for display
    static formatAddress(address) {
        if (!address) return 'Unknown';
        if (address.length <= 10) return address;
        return `${address.substring(0, 6)}...${address.substring(address.length - 4)}`;
    }
    
    // Copy to clipboard with feedback
    static async copyToClipboard(text, feedbackElement = null) {
        try {
            await navigator.clipboard.writeText(text);
            if (feedbackElement) {
                const originalText = feedbackElement.innerHTML;
                feedbackElement.innerHTML = '<i class="fas fa-check mr-1"></i>Copied!';
                feedbackElement.classList.add('text-green-400');
                
                setTimeout(() => {
                    feedbackElement.innerHTML = originalText;
                    feedbackElement.classList.remove('text-green-400');
                }, 2000);
            }
            return true;
        } catch (error) {
            console.error('Failed to copy to clipboard:', error);
            return false;
        }
    }
    
    // Generate QR code for address
    static generateQRCode(address, size = 200) {
        // Using QRCode.js library (would need to be included)
        const qrContainer = document.createElement('div');
        qrContainer.className = 'flex justify-center p-4';
        
        if (typeof QRCode !== 'undefined') {
            new QRCode(qrContainer, {
                text: address,
                width: size,
                height: size,
                colorDark: '#000000',
                colorLight: '#ffffff',
                correctLevel: QRCode.CorrectLevel.H
            });
        } else {
            // Fallback to text display
            qrContainer.innerHTML = `
                <div class="bg-white p-4 rounded-lg text-center">
                    <div class="text-black font-mono text-sm break-all">${address}</div>
                    <div class="text-gray-600 text-xs mt-2">QR Code library not loaded</div>
                </div>
            `;
        }
        
        return qrContainer;
    }
    
    // Format currency amounts
    static formatCurrency(amount, currency = 'LKS', decimals = 4) {
        if (typeof amount !== 'number') {
            amount = parseFloat(amount) || 0;
        }
        
        return `${amount.toFixed(decimals)} ${currency}`;
    }
    
    // Calculate transaction fee (always 0 for LKS Network)
    static calculateTransactionFee(walletType, transactionType = 'transfer') {
        if (walletType === 'lks-wallet' || walletType === 'metamask') {
            return 0; // Zero fees on LKS Network
        }
        
        // Other networks have their own fee structures
        const baseFees = {
            'phantom': 0.000005, // SOL
            'xaman': 0.00001 // XRP
        };
        
        return baseFees[walletType] || 0;
    }
    
    // Network status indicators
    static getNetworkStatus(walletType) {
        // In production, this would check actual network status
        return {
            status: 'online',
            latency: Math.floor(Math.random() * 100) + 50, // ms
            blockHeight: Math.floor(Math.random() * 1000000) + 15000000,
            tps: walletType === 'lks-wallet' ? 65000 : 
                 walletType === 'phantom' ? 3000 : 
                 walletType === 'xaman' ? 1500 : 15
        };
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = WalletUtils;
}
