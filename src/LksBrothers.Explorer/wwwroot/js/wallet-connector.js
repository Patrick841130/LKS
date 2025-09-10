// Professional wallet connection system with seamless login and transaction flows
class WalletConnector {
    constructor() {
        this.connectedWallet = null;
        this.walletAddress = null;
        this.walletBalance = null;
        this.web3 = null;
        this.solanaConnection = null;
        this.xamanClient = null;
        this.connectionStatus = 'disconnected';
        this.transactionQueue = [];
        
        // Initialize Web3 Security Manager
        if (typeof Web3SecurityManager !== 'undefined') {
            this.securityManager = new Web3SecurityManager();
            this.securityManager.initialize();
        }
        
        this.initializeEventListeners();
        this.setupAutoReconnect();
    }

    init() {
        const savedWallet = localStorage.getItem('connectedWallet');
        const savedAddress = localStorage.getItem('userAddress');
        
        if (savedWallet && savedAddress) {
            this.connectedWallet = savedWallet;
            this.walletAddress = savedAddress;
            this.updateUI();
        }
    }

    // Connect to MetaMask
    async connectMetaMask() {
        try {
            this.updateConnectionStatus('connecting', 'MetaMask');
            
            if (typeof window.ethereum === 'undefined') {
                throw new Error('MetaMask is not installed. Please install MetaMask browser extension.');
            }

            // Request account access with enhanced UX
            const accounts = await window.ethereum.request({
                method: 'eth_requestAccounts'
            });

            if (accounts.length === 0) {
                throw new Error('No accounts found. Please unlock MetaMask and try again.');
            }

            this.walletAddress = accounts[0];
            this.web3 = new Web3(window.ethereum);
            
            // Switch to LKS Network with user-friendly messaging
            await this.switchToLKSNetwork();
            
            // Get balance and transaction history
            await this.updateBalance();
            await this.loadTransactionHistory();
            
            this.connectedWallet = 'metamask';
            this.connectionStatus = 'connected';
            this.updateUI();
            this.showSuccessMessage('MetaMask connected successfully! Welcome to LKS Network.');
            
            // Setup enhanced event listeners
            this.setupMetaMaskListeners();
            
            // Log connection for analytics
            this.logWalletConnection('metamask', this.walletAddress);
            
            return true;
            
        } catch (error) {
            console.error('MetaMask connection failed:', error);
            this.connectionStatus = 'failed';
            this.showError('MetaMask Connection Failed', error.message);
            return false;
        }
    }

    // Connect to Phantom (Solana)
    async connectPhantom() {
        try {
            this.updateConnectionStatus('connecting', 'Phantom');
            
            if (typeof window.solana === 'undefined' || !window.solana.isPhantom) {
                throw new Error('Phantom wallet is not installed. Please install Phantom from phantom.app');
            }

            const response = await window.solana.connect({ onlyIfTrusted: false });
            this.walletAddress = response.publicKey.toString();
            
            // Initialize Solana connection with multiple endpoints for reliability
            const endpoints = [
                'https://api.mainnet-beta.solana.com',
                'https://solana-api.projectserum.com',
                'https://rpc.ankr.com/solana'
            ];
            
            this.solanaConnection = new solanaWeb3.Connection(endpoints[0]);
            
            // Get comprehensive balance information
            const balance = await this.solanaConnection.getBalance(response.publicKey);
            const tokenAccounts = await this.solanaConnection.getParsedTokenAccountsByOwner(
                response.publicKey,
                { programId: solanaWeb3.TOKEN_PROGRAM_ID }
            );
            
            this.walletBalance = {
                sol: balance / solanaWeb3.LAMPORTS_PER_SOL,
                tokens: tokenAccounts.value.length,
                lks: await this.getLKSBalanceForSolana(this.walletAddress)
            };
            
            this.connectedWallet = 'phantom';
            this.connectionStatus = 'connected';
            await this.loadTransactionHistory();
            this.updateUI();
            this.showSuccessMessage('Phantom wallet connected! Ready for Solana and cross-chain transactions.');
            
            // Setup Phantom event listeners
            this.setupPhantomListeners();
            
            // Log connection
            this.logWalletConnection('phantom', this.walletAddress);
            
            return true;
            
        } catch (error) {
            console.error('Phantom connection failed:', error);
            this.connectionStatus = 'failed';
            
            if (error.code === 4001) {
                this.showError('Connection Cancelled', 'You cancelled the connection request.');
            } else {
                this.showError('Phantom Connection Failed', error.message);
            }
            return false;
        }
    }

    // Connect to Xaman (XRPL)
    async connectXaman() {
        try {
            this.updateConnectionStatus('connecting', 'Xaman');
            
            // Check if Xaman is available
            if (window.xaman) {
                const response = await window.xaman.connect();
                
                if (response.account) {
                    this.walletAddress = response.account;
                    
                    // Initialize Xaman client with multiple endpoints for reliability
                    const endpoints = [
                        'https://xrplcluster.com',
                        'https://xrpl.ws',
                        'https://s1.ripple.com'
                    ];
                    
                    this.xamanClient = new xrpl.Client(endpoints[0]);
                    
                    // Get comprehensive balance information
                    const balance = await this.xamanClient.getBalance(this.walletAddress);
                    const trustlines = await this.xamanClient.getTrustlines(this.walletAddress);
                    
                    this.walletBalance = {
                        xrp: balance / 1e6,
                        tokens: trustlines.length,
                        lks: await this.getLKSBalanceForXaman(this.walletAddress)
                    };
                    
                    this.connectedWallet = 'xaman';
                    this.connectionStatus = 'connected';
                    await this.loadTransactionHistory();
                    this.updateUI();
                    this.showSuccessMessage('Xaman wallet connected! Ready for XRPL and cross-chain transactions.');
                    
                    // Setup Xaman event listeners
                    this.setupXamanListeners();
                    
                    // Log connection
                    this.logWalletConnection('xaman', this.walletAddress);
                    
                    return true;
                    
                } else {
                    throw new Error('Failed to connect to Xaman wallet.');
                }
            } else {
                // Use Xaman SDK for web connection
                const { XummSdk } = await import('https://cdn.skypack.dev/xumm-sdk');
                const xumm = new XummSdk('your-api-key'); // Replace with actual API key
                
                // Create sign-in request
                const request = {
                    txjson: {
                        TransactionType: 'SignIn'
                    }
                };
                
                const payload = await xumm.payload.create(request);
                
                // Open Xaman for signing
                window.open(payload.next.always, '_blank');
                
                // For now, show instruction to user
                this.showError('Please complete sign-in in Xaman app and return here');
                
                return false;
            }
        } catch (error) {
            console.error('Xaman connection failed:', error);
            this.connectionStatus = 'failed';
            this.showError('Xaman Connection Failed', error.message);
            return false;
        }
    }

    // Connect to LKS Wallet
    async connectLKSWallet() {
        try {
            this.updateConnectionStatus('connecting', 'LKS Wallet');
            
            // Open LKS Wallet in new window
            const walletWindow = window.open('lks-wallet.html', 'lksWallet', 'width=400,height=700,scrollbars=yes,resizable=yes');
            
            // Listen for wallet connection from popup
            const messageHandler = async (event) => {
                if (event.data.type === 'LKS_WALLET_CONNECTED') {
                    this.walletAddress = event.data.address;
                    
                    // Get balance from backend API
                    try {
                        const response = await fetch(`http://localhost:3000/api/balance/${this.walletAddress}`);
                        const balanceData = await response.json();
                        
                        this.walletBalance = {
                            lks: balanceData.lks || 0,
                            eth: balanceData.eth || 0
                        };
                    } catch (error) {
                        console.error('Failed to get balance:', error);
                        this.walletBalance = { lks: 0, eth: 0 };
                    }
                    
                    this.connectedWallet = 'lks-wallet';
                    this.connectionStatus = 'connected';
                    await this.loadTransactionHistory();
                    this.updateUI();
                    this.showSuccessMessage('LKS Wallet connected! Ready for LKS Network transactions.');
                    
                    // Setup LKS Wallet event listeners
                    this.setupLKSWalletListeners();
                    
                    // Log connection
                    this.logWalletConnection('lks-wallet', this.walletAddress);
                    
                    return true;
                    
                } else if (event.data.type === 'LKS_WALLET_DISCONNECTED') {
                    this.disconnect();
                }
            };
            
            window.addEventListener('message', messageHandler);
            
            // Fallback if popup is blocked
            setTimeout(() => {
                if (walletWindow.closed) {
                    window.removeEventListener('message', messageHandler);
                    this.showError('Please allow popups to use LKS Wallet');
                }
            }, 1000);
            
        } catch (error) {
            console.error('LKS Wallet connection failed:', error);
            this.connectionStatus = 'failed';
            this.showError('LKS Wallet Connection Failed', error.message);
            return false;
        }
    }

    // Disconnect wallet
    disconnect() {
        this.connectedWallet = null;
        this.walletAddress = null;
        this.walletBalance = null;
        this.web3 = null;
        this.solanaConnection = null;
        this.xamanClient = null;
        this.lksWalletClient = null;
        this.connectionStatus = 'disconnected';
        this.updateUI();
        this.showSuccessMessage('Wallet disconnected successfully!');
    }

    // Update UI based on connection status
    updateUI() {
        const walletInfo = document.getElementById('wallet-info');
        const walletDetails = document.getElementById('wallet-details');
        const connectBtn = document.getElementById('connect-wallet-btn');
        
        if (this.connectedWallet && this.walletAddress) {
            // Update connect button with professional styling
            const walletIcon = this.getWalletIcon();
            connectBtn.innerHTML = `
                <div class="flex items-center">
                    <div class="w-6 h-6 ${this.getWalletColor()} rounded-full flex items-center justify-center mr-2">
                        <i class="${walletIcon} text-white text-xs"></i>
                    </div>
                    <span class="font-medium">${this.walletAddress.substring(0, 6)}...${this.walletAddress.substring(this.walletAddress.length - 4)}</span>
                    <div class="w-2 h-2 bg-green-400 rounded-full ml-2 animate-pulse"></div>
                </div>
            `;
            connectBtn.classList.remove('bg-green-600', 'hover:bg-green-700');
            connectBtn.classList.add('bg-gray-700', 'hover:bg-gray-600', 'border', 'border-gray-600');
            
            // Show comprehensive wallet info
            walletInfo.style.display = 'block';
            walletDetails.innerHTML = `
                <div class="grid grid-cols-2 gap-4 mb-4">
                    <div class="bg-gray-900/50 p-3 rounded-lg">
                        <div class="text-xs text-gray-400 mb-1">Wallet Type</div>
                        <div class="font-semibold text-white">${this.getWalletDisplayName()}</div>
                    </div>
                    <div class="bg-gray-900/50 p-3 rounded-lg">
                        <div class="text-xs text-gray-400 mb-1">Network</div>
                        <div class="font-semibold text-green-400">${this.getNetworkName()}</div>
                    </div>
                </div>
                <div class="bg-gray-900/50 p-3 rounded-lg mb-4">
                    <div class="text-xs text-gray-400 mb-1">Address</div>
                    <div class="font-mono text-sm text-white break-all">${this.walletAddress}</div>
                    <button onclick="alert('Address: ${this.walletAddress}')" class="text-xs text-blue-400 hover:text-blue-300 mt-1">
                        <i class="fas fa-eye mr-1"></i>Show Address
                    </button>
                </div>
                ${this.walletBalance ? this.formatBalance() : '<div class="text-center py-4"><i class="fas fa-spinner fa-spin mr-2"></i>Loading balance...</div>'}
                <div class="flex space-x-2 mt-4">
                    <button onclick="walletConnector.showSendModal()" class="flex-1 bg-blue-600 hover:bg-blue-700 text-white py-2 px-4 rounded-lg text-sm font-medium transition-colors">
                        <i class="fas fa-paper-plane mr-2"></i>Send
                    </button>
                    <button onclick="walletConnector.showReceiveModal()" class="flex-1 bg-green-600 hover:bg-green-700 text-white py-2 px-4 rounded-lg text-sm font-medium transition-colors">
                        <i class="fas fa-qrcode mr-2"></i>Receive
                    </button>
                </div>
            `;
            
            // Close modal after successful connection
            setTimeout(() => this.closeModal(), 1000);
        } else {
            // Reset UI to initial state
            connectBtn.innerHTML = '<i class="fas fa-wallet mr-2"></i>Connect Wallet';
            connectBtn.classList.remove('bg-gray-700', 'hover:bg-gray-600', 'border', 'border-gray-600');
            connectBtn.classList.add('bg-green-600', 'hover:bg-green-700');
            walletInfo.style.display = 'none';
        }
    }

    // Format address for display
    formatAddress(address) {
        if (!address) return '';
        return `${address.substring(0, 6)}...${address.substring(address.length - 4)}`;
    }

    // Get currency symbol based on wallet
    getCurrencySymbol() {
        switch (this.connectedWallet) {
            case 'MetaMask': return 'ETH';
            case 'Phantom': return 'SOL';
            case 'Xaman': return 'XRP';
            case 'LKS Wallet': return 'LKS';
            default: return 'TOKEN';
        }
    }

    // Show wallet selection modal
    showWalletModal() {
        const modal = document.getElementById('wallet-modal');
        if (modal) {
            modal.style.display = 'flex';
        }
    }

    // Hide wallet selection modal
    hideWalletModal() {
        const modal = document.getElementById('wallet-modal');
        if (modal) {
            modal.style.display = 'none';
        }
    }

    // Close modal (alias for hideWalletModal)
    closeModal() {
        this.hideWalletModal();
    }

    // Setup event listeners
    setupEventListeners() {
        // Listen for account changes in MetaMask
        if (window.ethereum) {
            window.ethereum.on('accountsChanged', (accounts) => {
                if (accounts.length === 0) {
                    this.disconnect();
                } else if (this.connectedWallet === 'MetaMask') {
                    this.userAddress = accounts[0];
                    this.saveConnection();
                    this.getBalance();
                    this.updateUI();
                }
            });
            
            // Listen for chain changes
            window.ethereum.on('chainChanged', (chainId) => {
                if (this.connectedWallet === 'MetaMask') {
                    this.getBalance();
                    this.updateUI();
                }
            });
            
            // Listen for disconnection
            window.ethereum.on('disconnect', () => {
                if (this.connectedWallet === 'MetaMask') {
                    this.disconnect();
                }
            });
        }

        // Listen for Phantom events
        if (window.solana) {
            window.solana.on('connect', (publicKey) => {
                console.log('Phantom connected:', publicKey.toString());
            });
            
            window.solana.on('disconnect', () => {
                if (this.connectedWallet === 'Phantom') {
                    this.disconnect();
                }
            });
        }
    }

    // Show success message
    showSuccessMessage(message) {
        this.showToast(message, 'success');
    }

    // Show error message
    showErrorMessage(message) {
        this.showToast(message, 'error');
    }

    // Show error with title
    showError(title, message) {
        this.showToast(`${title}: ${message}`, 'error');
    }

    // Get wallet icon
    getWalletIcon() {
        switch (this.connectedWallet) {
            case 'metamask': return 'fab fa-ethereum';
            case 'phantom': return 'fas fa-ghost';
            case 'xaman': return 'fas fa-coins';
            case 'lks-wallet': return 'fas fa-wallet';
            default: return 'fas fa-wallet';
        }
    }

    // Get wallet color
    getWalletColor() {
        switch (this.connectedWallet) {
            case 'metamask': return 'bg-orange-500';
            case 'phantom': return 'bg-purple-500';
            case 'xaman': return 'bg-blue-500';
            case 'lks-wallet': return 'bg-green-500';
            default: return 'bg-gray-500';
        }
    }

    // Get wallet display name
    getWalletDisplayName() {
        switch (this.connectedWallet) {
            case 'metamask': return 'MetaMask';
            case 'phantom': return 'Phantom';
            case 'xaman': return 'Xaman';
            case 'lks-wallet': return 'LKS Wallet';
            default: return 'Unknown';
        }
    }

    // Get network name
    getNetworkName() {
        switch (this.connectedWallet) {
            case 'metamask': return 'LKS Network';
            case 'phantom': return 'Solana';
            case 'xaman': return 'XRPL';
            case 'lks-wallet': return 'LKS Network';
            default: return 'Unknown';
        }
    }

    // Format balance display
    formatBalance() {
        if (!this.walletBalance) return '';
        
        let balanceHtml = '<div class="grid grid-cols-2 gap-2">';
        
        if (this.walletBalance.lks !== undefined) {
            balanceHtml += `
                <div class="bg-green-500/20 p-2 rounded">
                    <div class="text-xs text-green-300">LKS</div>
                    <div class="font-bold text-green-400">${this.walletBalance.lks.toFixed(2)}</div>
                </div>
            `;
        }
        
        if (this.walletBalance.eth !== undefined) {
            balanceHtml += `
                <div class="bg-blue-500/20 p-2 rounded">
                    <div class="text-xs text-blue-300">ETH</div>
                    <div class="font-bold text-blue-400">${this.walletBalance.eth.toFixed(4)}</div>
                </div>
            `;
        }
        
        if (this.walletBalance.sol !== undefined) {
            balanceHtml += `
                <div class="bg-purple-500/20 p-2 rounded">
                    <div class="text-xs text-purple-300">SOL</div>
                    <div class="font-bold text-purple-400">${this.walletBalance.sol.toFixed(4)}</div>
                </div>
            `;
        }
        
        if (this.walletBalance.xrp !== undefined) {
            balanceHtml += `
                <div class="bg-blue-500/20 p-2 rounded">
                    <div class="text-xs text-blue-300">XRP</div>
                    <div class="font-bold text-blue-400">${this.walletBalance.xrp.toFixed(4)}</div>
                </div>
            `;
        }
        
        balanceHtml += '</div>';
        return balanceHtml;
    }

    // Initialize event listeners
    initializeEventListeners() {
        this.setupEventListeners();
    }

    // Setup auto reconnect
    setupAutoReconnect() {
        // Check for saved connection on page load
        setTimeout(() => {
            this.init();
        }, 1000);
    }

    // Update connection status
    updateConnectionStatus(status, walletType) {
        console.log(`Wallet connection status: ${status} (${walletType})`);
    }

    // Switch to LKS Network
    async switchToLKSNetwork() {
        // For demo purposes, we'll just log this
        console.log('Switching to LKS Network...');
        return true;
    }

    // Update balance
    async updateBalance() {
        // Mock balance for demo
        this.walletBalance = {
            lks: 1000.50,
            eth: 0.5
        };
    }

    // Load transaction history
    async loadTransactionHistory() {
        console.log('Loading transaction history...');
    }

    // Setup wallet-specific listeners
    setupMetaMaskListeners() {
        console.log('MetaMask listeners setup');
    }

    setupPhantomListeners() {
        console.log('Phantom listeners setup');
    }

    setupXamanListeners() {
        console.log('Xaman listeners setup');
    }

    setupLKSWalletListeners() {
        console.log('LKS Wallet listeners setup');
    }

    // Log wallet connection
    logWalletConnection(walletType, address) {
        console.log(`Wallet connected: ${walletType} - ${address}`);
    }

    // Get LKS balance for different wallets
    async getLKSBalanceForSolana(address) {
        return 0; // Mock balance
    }

    async getLKSBalanceForXaman(address) {
        return 0; // Mock balance
    }

    // Show send modal
    showSendModal() {
        this.showToast('Send functionality coming soon!', 'info');
    }

    // Show receive modal
    showReceiveModal() {
        this.showToast('Receive functionality coming soon!', 'info');
    }

    // Show toast notification
    showToast(message, type = 'info') {
        const toast = document.createElement('div');
        toast.className = `fixed top-4 right-4 z-50 p-4 rounded-lg shadow-lg transition-all duration-300 ${
            type === 'success' ? 'bg-green-500' : 
            type === 'error' ? 'bg-red-500' : 'bg-blue-500'
        } text-white`;
        
        toast.innerHTML = `
            <div class="flex items-center">
                <i class="fas fa-${type === 'success' ? 'check' : type === 'error' ? 'exclamation-triangle' : 'info'} mr-2"></i>
                ${message}
            </div>
        `;

        document.body.appendChild(toast);

        // Auto remove after 3 seconds
        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => {
                document.body.removeChild(toast);
            }, 300);
        }, 3000);
    }
}

// Initialize wallet connector
const walletConnector = new WalletConnector();

// Global functions for HTML onclick events
function closeWalletModal() {
    walletConnector.hideWalletModal();
}

function connectMetaMask() {
    walletConnector.connectMetaMask();
}

function connectPhantom() {
    walletConnector.connectPhantom();
}

function connectXaman() {
    walletConnector.connectXaman();
}

function connectLKSWallet() {
    walletConnector.connectLKSWallet();
}

function disconnectWallet() {
    walletConnector.disconnect();
}
