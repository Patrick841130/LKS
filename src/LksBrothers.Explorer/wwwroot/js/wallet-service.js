/**
 * LKS Network Wallet Connection Service
 * Handles wallet connections, authentication, and blockchain interactions
 */

class LKSWalletService {
    constructor() {
        this.isConnected = false;
        this.currentAccount = null;
        this.provider = null;
        this.networkConfig = {
            chainId: '0x4C4B53', // LKS in hex
            chainName: 'LKS Network',
            nativeCurrency: {
                name: 'LKS Coin',
                symbol: 'LKS',
                decimals: 18
            },
            rpcUrls: ['http://localhost:8545', 'https://rpc.lksnetwork.io'],
            blockExplorerUrls: ['http://localhost:3001']
        };
        this.eventListeners = new Map();
    }

    /**
     * Initialize wallet service and check for existing connections
     */
    async initialize() {
        try {
            // Check if MetaMask is installed
            if (typeof window.ethereum !== 'undefined') {
                this.provider = window.ethereum;
                
                // Check if already connected
                const accounts = await this.provider.request({ method: 'eth_accounts' });
                if (accounts.length > 0) {
                    this.currentAccount = accounts[0];
                    this.isConnected = true;
                    this.emit('accountChanged', this.currentAccount);
                }

                // Set up event listeners
                this.setupEventListeners();
            }

            // Initialize WalletConnect if MetaMask not available
            await this.initializeWalletConnect();
            
            return this.isConnected;
        } catch (error) {
            console.error('Failed to initialize wallet service:', error);
            return false;
        }
    }

    /**
     * Connect to MetaMask wallet
     */
    async connectMetaMask() {
        try {
            if (!window.ethereum) {
                throw new Error('MetaMask not installed');
            }

            // Request account access
            const accounts = await window.ethereum.request({
                method: 'eth_requestAccounts'
            });

            if (accounts.length === 0) {
                throw new Error('No accounts found');
            }

            this.currentAccount = accounts[0];
            this.isConnected = true;
            this.provider = window.ethereum;

            // Add LKS Network to MetaMask
            await this.addLKSNetwork();

            this.emit('connected', {
                account: this.currentAccount,
                provider: 'MetaMask'
            });

            return {
                success: true,
                account: this.currentAccount,
                provider: 'MetaMask'
            };
        } catch (error) {
            console.error('MetaMask connection failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    /**
     * Initialize WalletConnect
     */
    async initializeWalletConnect() {
        try {
            // WalletConnect v2 implementation
            if (typeof window.WalletConnect !== 'undefined') {
                const { EthereumProvider } = window.WalletConnect;
                
                this.walletConnectProvider = await EthereumProvider.init({
                    projectId: 'lks-network-explorer', // Replace with actual project ID
                    chains: [parseInt(this.networkConfig.chainId, 16)],
                    showQrModal: true,
                    rpcMap: {
                        [parseInt(this.networkConfig.chainId, 16)]: this.networkConfig.rpcUrls[0]
                    }
                });

                this.walletConnectProvider.on('display_uri', (uri) => {
                    console.log('WalletConnect URI:', uri);
                });

                this.walletConnectProvider.on('connect', (accounts) => {
                    this.currentAccount = accounts[0];
                    this.isConnected = true;
                    this.provider = this.walletConnectProvider;
                    this.emit('connected', {
                        account: this.currentAccount,
                        provider: 'WalletConnect'
                    });
                });
            }
        } catch (error) {
            console.error('WalletConnect initialization failed:', error);
        }
    }

    /**
     * Connect via WalletConnect
     */
    async connectWalletConnect() {
        try {
            if (!this.walletConnectProvider) {
                await this.initializeWalletConnect();
            }

            if (!this.walletConnectProvider) {
                throw new Error('WalletConnect not available');
            }

            await this.walletConnectProvider.connect();

            return {
                success: true,
                account: this.currentAccount,
                provider: 'WalletConnect'
            };
        } catch (error) {
            console.error('WalletConnect connection failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    /**
     * Add LKS Network to wallet
     */
    async addLKSNetwork() {
        try {
            await this.provider.request({
                method: 'wallet_addEthereumChain',
                params: [this.networkConfig]
            });
            return true;
        } catch (error) {
            console.error('Failed to add LKS Network:', error);
            return false;
        }
    }

    /**
     * Switch to LKS Network
     */
    async switchToLKSNetwork() {
        try {
            await this.provider.request({
                method: 'wallet_switchEthereumChain',
                params: [{ chainId: this.networkConfig.chainId }]
            });
            return true;
        } catch (error) {
            if (error.code === 4902) {
                // Network not added, try to add it
                return await this.addLKSNetwork();
            }
            console.error('Failed to switch network:', error);
            return false;
        }
    }

    /**
     * Get account balance
     */
    async getBalance(account = this.currentAccount) {
        try {
            if (!this.provider || !account) {
                throw new Error('Wallet not connected');
            }

            const balance = await this.provider.request({
                method: 'eth_getBalance',
                params: [account, 'latest']
            });

            // Convert from wei to LKS
            const balanceInLKS = parseInt(balance, 16) / Math.pow(10, 18);
            return balanceInLKS;
        } catch (error) {
            console.error('Failed to get balance:', error);
            return 0;
        }
    }

    /**
     * Send LKS transaction
     */
    async sendTransaction(to, amount, data = '0x') {
        try {
            if (!this.provider || !this.currentAccount) {
                throw new Error('Wallet not connected');
            }

            // Convert amount to wei
            const amountInWei = '0x' + (amount * Math.pow(10, 18)).toString(16);

            const transactionParameters = {
                from: this.currentAccount,
                to: to,
                value: amountInWei,
                data: data,
                gas: '0x5208', // 21000 gas limit
                gasPrice: '0x0' // Zero gas price for LKS Network
            };

            const txHash = await this.provider.request({
                method: 'eth_sendTransaction',
                params: [transactionParameters]
            });

            return {
                success: true,
                transactionHash: txHash
            };
        } catch (error) {
            console.error('Transaction failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    /**
     * Sign message for authentication
     */
    async signMessage(message) {
        try {
            if (!this.provider || !this.currentAccount) {
                throw new Error('Wallet not connected');
            }

            const signature = await this.provider.request({
                method: 'personal_sign',
                params: [message, this.currentAccount]
            });

            return {
                success: true,
                signature: signature,
                message: message,
                account: this.currentAccount
            };
        } catch (error) {
            console.error('Message signing failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    /**
     * Authenticate with LKS Network backend
     */
    async authenticate() {
        try {
            const timestamp = Date.now();
            const message = `Sign this message to authenticate with LKS Network Explorer.\n\nTimestamp: ${timestamp}\nAccount: ${this.currentAccount}`;
            
            const signResult = await this.signMessage(message);
            if (!signResult.success) {
                throw new Error(signResult.error);
            }

            // Send authentication request to backend
            const authResponse = await fetch('/api/auth/wallet', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    account: this.currentAccount,
                    signature: signResult.signature,
                    message: message,
                    timestamp: timestamp
                })
            });

            const authData = await authResponse.json();
            
            if (authData.success) {
                // Store authentication token
                localStorage.setItem('lks_auth_token', authData.token);
                localStorage.setItem('lks_auth_account', this.currentAccount);
                
                this.emit('authenticated', {
                    account: this.currentAccount,
                    token: authData.token
                });

                return {
                    success: true,
                    token: authData.token,
                    account: this.currentAccount
                };
            } else {
                throw new Error(authData.error || 'Authentication failed');
            }
        } catch (error) {
            console.error('Authentication failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    /**
     * Disconnect wallet
     */
    async disconnect() {
        try {
            if (this.walletConnectProvider && this.provider === this.walletConnectProvider) {
                await this.walletConnectProvider.disconnect();
            }

            this.isConnected = false;
            this.currentAccount = null;
            this.provider = null;

            // Clear authentication
            localStorage.removeItem('lks_auth_token');
            localStorage.removeItem('lks_auth_account');

            this.emit('disconnected');

            return { success: true };
        } catch (error) {
            console.error('Disconnect failed:', error);
            return {
                success: false,
                error: error.message
            };
        }
    }

    /**
     * Setup event listeners for wallet events
     */
    setupEventListeners() {
        if (this.provider && this.provider.on) {
            this.provider.on('accountsChanged', (accounts) => {
                if (accounts.length === 0) {
                    this.disconnect();
                } else {
                    this.currentAccount = accounts[0];
                    this.emit('accountChanged', this.currentAccount);
                }
            });

            this.provider.on('chainChanged', (chainId) => {
                this.emit('chainChanged', chainId);
                // Reload page if not on LKS Network
                if (chainId !== this.networkConfig.chainId) {
                    window.location.reload();
                }
            });

            this.provider.on('disconnect', () => {
                this.disconnect();
            });
        }
    }

    /**
     * Event system for wallet events
     */
    on(event, callback) {
        if (!this.eventListeners.has(event)) {
            this.eventListeners.set(event, []);
        }
        this.eventListeners.get(event).push(callback);
    }

    emit(event, data) {
        if (this.eventListeners.has(event)) {
            this.eventListeners.get(event).forEach(callback => {
                try {
                    callback(data);
                } catch (error) {
                    console.error(`Error in event listener for ${event}:`, error);
                }
            });
        }
    }

    /**
     * Get connection status
     */
    getStatus() {
        return {
            isConnected: this.isConnected,
            account: this.currentAccount,
            provider: this.provider ? (this.provider === window.ethereum ? 'MetaMask' : 'WalletConnect') : null,
            networkSupported: true
        };
    }

    /**
     * Get network information
     */
    getNetworkInfo() {
        return this.networkConfig;
    }
}

// Create global wallet service instance
window.lksWallet = new LKSWalletService();

// Auto-initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', async () => {
    await window.lksWallet.initialize();
});
