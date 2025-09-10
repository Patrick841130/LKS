/**
 * LKS Network Wallet UI Components
 * Handles wallet connection interface and user interactions
 */

class LKSWalletUI {
    constructor() {
        this.isInitialized = false;
        this.currentModal = null;
        this.walletService = window.lksWallet;
        this.elements = {};
    }

    /**
     * Initialize wallet UI components
     */
    async initialize() {
        if (this.isInitialized) return;

        await this.createWalletButton();
        await this.createWalletModal();
        await this.createWalletInfo();
        this.setupEventListeners();
        this.isInitialized = true;

        // Update UI based on current wallet status
        await this.updateWalletStatus();
    }

    /**
     * Create wallet connect button
     */
    async createWalletButton() {
        const walletButton = document.createElement('button');
        walletButton.id = 'wallet-connect-btn';
        walletButton.className = 'wallet-connect-btn';
        walletButton.innerHTML = `
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M21 16V8C21 6.89543 20.1046 6 19 6H5C3.89543 6 3 6.89543 3 8V16C3 17.1046 3.89543 18 5 18H19C20.1046 18 21 17.1046 21 16Z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                <path d="M7 10H17" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            <span id="wallet-btn-text">Connect Wallet</span>
        `;

        // Add to navigation or header
        const nav = document.querySelector('.navbar') || document.querySelector('nav') || document.querySelector('header');
        if (nav) {
            nav.appendChild(walletButton);
        } else {
            // Create a floating wallet button
            walletButton.style.position = 'fixed';
            walletButton.style.top = '20px';
            walletButton.style.right = '20px';
            walletButton.style.zIndex = '1000';
            document.body.appendChild(walletButton);
        }

        this.elements.walletButton = walletButton;
    }

    /**
     * Create wallet connection modal
     */
    async createWalletModal() {
        const modal = document.createElement('div');
        modal.id = 'wallet-modal';
        modal.className = 'wallet-modal';
        modal.innerHTML = `
            <div class="wallet-modal-overlay">
                <div class="wallet-modal-content">
                    <div class="wallet-modal-header">
                        <h3>Connect to LKS Network</h3>
                        <button class="wallet-modal-close">&times;</button>
                    </div>
                    <div class="wallet-modal-body">
                        <div class="wallet-options">
                            <button class="wallet-option" data-wallet="metamask">
                                <img src="data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzIiIGhlaWdodD0iMzIiIHZpZXdCb3g9IjAgMCAzMiAzMiIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPHBhdGggZD0iTTI5LjUgMTZDMjkuNSAyMy40NTU4IDIzLjQ1NTggMjkuNSAxNiAyOS41QzguNTQ0MTYgMjkuNSAyLjUgMjMuNDU1OCAyLjUgMTZDMi41IDguNTQ0MTYgOC41NDQxNiAyLjUgMTYgMi41QzIzLjQ1NTggMi41IDI5LjUgOC41NDQxNiAyOS41IDE2WiIgZmlsbD0iI0Y2ODUxQiIgc3Ryb2tlPSIjRjY4NTFCIi8+Cjwvc3ZnPgo=" alt="MetaMask">
                                <div>
                                    <div class="wallet-name">MetaMask</div>
                                    <div class="wallet-description">Connect using browser wallet</div>
                                </div>
                            </button>
                            <button class="wallet-option" data-wallet="walletconnect">
                                <img src="data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMzIiIGhlaWdodD0iMzIiIHZpZXdCb3g9IjAgMCAzMiAzMiIgZmlsbD0ibm9uZSIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj4KPHBhdGggZD0iTTI5LjUgMTZDMjkuNSAyMy40NTU4IDIzLjQ1NTggMjkuNSAxNiAyOS41QzguNTQ0MTYgMjkuNSAyLjUgMjMuNDU1OCAyLjUgMTZDMi41IDguNTQ0MTYgOC41NDQxNiAyLjUgMTYgMi41QzIzLjQ1NTggMi41IDI5LjUgOC41NDQxNiAyOS41IDE2WiIgZmlsbD0iIzM5OTlGRiIgc3Ryb2tlPSIjMzk5OUZGIi8+Cjwvc3ZnPgo=" alt="WalletConnect">
                                <div>
                                    <div class="wallet-name">WalletConnect</div>
                                    <div class="wallet-description">Connect using mobile wallet</div>
                                </div>
                            </button>
                        </div>
                        <div class="wallet-status" id="wallet-connection-status"></div>
                    </div>
                </div>
            </div>
        `;

        document.body.appendChild(modal);
        this.elements.modal = modal;
    }

    /**
     * Create wallet info display
     */
    async createWalletInfo() {
        const walletInfo = document.createElement('div');
        walletInfo.id = 'wallet-info';
        walletInfo.className = 'wallet-info hidden';
        walletInfo.innerHTML = `
            <div class="wallet-info-content">
                <div class="wallet-account">
                    <div class="wallet-avatar"></div>
                    <div class="wallet-details">
                        <div class="wallet-address" id="wallet-address"></div>
                        <div class="wallet-balance" id="wallet-balance">0 LKS</div>
                    </div>
                </div>
                <div class="wallet-actions">
                    <button class="wallet-action-btn" id="wallet-send-btn">Send</button>
                    <button class="wallet-action-btn" id="wallet-receive-btn">Receive</button>
                    <button class="wallet-action-btn" id="wallet-disconnect-btn">Disconnect</button>
                </div>
            </div>
        `;

        // Add to a suitable location
        const container = document.querySelector('.container') || document.body;
        container.appendChild(walletInfo);
        this.elements.walletInfo = walletInfo;
    }

    /**
     * Setup event listeners
     */
    setupEventListeners() {
        // Wallet button click
        this.elements.walletButton?.addEventListener('click', () => {
            if (this.walletService.isConnected) {
                this.toggleWalletInfo();
            } else {
                this.showWalletModal();
            }
        });

        // Modal close
        const closeBtn = document.querySelector('.wallet-modal-close');
        closeBtn?.addEventListener('click', () => this.hideWalletModal());

        // Modal overlay click
        const overlay = document.querySelector('.wallet-modal-overlay');
        overlay?.addEventListener('click', (e) => {
            if (e.target === overlay) {
                this.hideWalletModal();
            }
        });

        // Wallet option clicks
        document.querySelectorAll('.wallet-option').forEach(option => {
            option.addEventListener('click', async () => {
                const walletType = option.dataset.wallet;
                await this.connectWallet(walletType);
            });
        });

        // Wallet action buttons
        document.getElementById('wallet-disconnect-btn')?.addEventListener('click', () => {
            this.disconnectWallet();
        });

        document.getElementById('wallet-send-btn')?.addEventListener('click', () => {
            this.showSendModal();
        });

        document.getElementById('wallet-receive-btn')?.addEventListener('click', () => {
            this.showReceiveModal();
        });

        // Wallet service events
        this.walletService.on('connected', (data) => {
            this.onWalletConnected(data);
        });

        this.walletService.on('disconnected', () => {
            this.onWalletDisconnected();
        });

        this.walletService.on('accountChanged', (account) => {
            this.onAccountChanged(account);
        });

        this.walletService.on('authenticated', (data) => {
            this.onAuthenticated(data);
        });
    }

    /**
     * Connect wallet
     */
    async connectWallet(walletType) {
        try {
            this.showConnectionStatus('Connecting...', 'loading');

            let result;
            if (walletType === 'metamask') {
                result = await this.walletService.connectMetaMask();
            } else if (walletType === 'walletconnect') {
                result = await this.walletService.connectWalletConnect();
            }

            if (result.success) {
                this.showConnectionStatus('Connected successfully!', 'success');
                
                // Authenticate with backend
                const authResult = await this.walletService.authenticate();
                if (authResult.success) {
                    this.showConnectionStatus('Authentication successful!', 'success');
                    setTimeout(() => this.hideWalletModal(), 1500);
                } else {
                    this.showConnectionStatus('Authentication failed: ' + authResult.error, 'error');
                }
            } else {
                this.showConnectionStatus('Connection failed: ' + result.error, 'error');
            }
        } catch (error) {
            this.showConnectionStatus('Connection failed: ' + error.message, 'error');
        }
    }

    /**
     * Disconnect wallet
     */
    async disconnectWallet() {
        try {
            await this.walletService.disconnect();
            this.hideWalletInfo();
        } catch (error) {
            console.error('Disconnect failed:', error);
        }
    }

    /**
     * Show wallet modal
     */
    showWalletModal() {
        this.elements.modal?.classList.add('show');
        this.currentModal = 'wallet';
    }

    /**
     * Hide wallet modal
     */
    hideWalletModal() {
        this.elements.modal?.classList.remove('show');
        this.currentModal = null;
        this.clearConnectionStatus();
    }

    /**
     * Toggle wallet info display
     */
    toggleWalletInfo() {
        const walletInfo = this.elements.walletInfo;
        if (walletInfo) {
            walletInfo.classList.toggle('hidden');
        }
    }

    /**
     * Show wallet info
     */
    showWalletInfo() {
        this.elements.walletInfo?.classList.remove('hidden');
    }

    /**
     * Hide wallet info
     */
    hideWalletInfo() {
        this.elements.walletInfo?.classList.add('hidden');
    }

    /**
     * Show connection status
     */
    showConnectionStatus(message, type) {
        const statusEl = document.getElementById('wallet-connection-status');
        if (statusEl) {
            statusEl.textContent = message;
            statusEl.className = `wallet-status ${type}`;
        }
    }

    /**
     * Clear connection status
     */
    clearConnectionStatus() {
        const statusEl = document.getElementById('wallet-connection-status');
        if (statusEl) {
            statusEl.textContent = '';
            statusEl.className = 'wallet-status';
        }
    }

    /**
     * Update wallet status in UI
     */
    async updateWalletStatus() {
        const status = this.walletService.getStatus();
        const btnText = document.getElementById('wallet-btn-text');
        
        if (status.isConnected && status.account) {
            // Update button
            if (btnText) {
                btnText.textContent = this.formatAddress(status.account);
            }
            this.elements.walletButton?.classList.add('connected');

            // Update wallet info
            await this.updateWalletInfo(status.account);
        } else {
            // Update button
            if (btnText) {
                btnText.textContent = 'Connect Wallet';
            }
            this.elements.walletButton?.classList.remove('connected');
            this.hideWalletInfo();
        }
    }

    /**
     * Update wallet info display
     */
    async updateWalletInfo(account) {
        const addressEl = document.getElementById('wallet-address');
        const balanceEl = document.getElementById('wallet-balance');

        if (addressEl) {
            addressEl.textContent = this.formatAddress(account);
        }

        // Get and display balance
        try {
            const balance = await this.walletService.getBalance(account);
            if (balanceEl) {
                balanceEl.textContent = `${balance.toFixed(4)} LKS`;
            }
        } catch (error) {
            console.error('Failed to get balance:', error);
            if (balanceEl) {
                balanceEl.textContent = '0 LKS';
            }
        }
    }

    /**
     * Format Ethereum address for display
     */
    formatAddress(address) {
        if (!address) return '';
        return `${address.substring(0, 6)}...${address.substring(address.length - 4)}`;
    }

    /**
     * Event handlers
     */
    onWalletConnected(data) {
        console.log('Wallet connected:', data);
        this.updateWalletStatus();
    }

    onWalletDisconnected() {
        console.log('Wallet disconnected');
        this.updateWalletStatus();
    }

    onAccountChanged(account) {
        console.log('Account changed:', account);
        this.updateWalletStatus();
    }

    onAuthenticated(data) {
        console.log('Authenticated:', data);
        // You can add additional UI updates here
        this.showNotification('Successfully authenticated with LKS Network!', 'success');
    }

    /**
     * Show notification
     */
    showNotification(message, type = 'info') {
        // Create notification element
        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.textContent = message;

        // Add to page
        document.body.appendChild(notification);

        // Auto remove after 3 seconds
        setTimeout(() => {
            notification.remove();
        }, 3000);
    }

    /**
     * Show send modal (placeholder)
     */
    showSendModal() {
        this.showNotification('Send functionality coming soon!', 'info');
    }

    /**
     * Show receive modal (placeholder)
     */
    showReceiveModal() {
        const account = this.walletService.currentAccount;
        if (account) {
            // Copy address to clipboard
            navigator.clipboard.writeText(account).then(() => {
                this.showNotification('Address copied to clipboard!', 'success');
            });
        }
    }
}

// Initialize wallet UI when DOM is loaded
document.addEventListener('DOMContentLoaded', async () => {
    window.lksWalletUI = new LKSWalletUI();
    await window.lksWalletUI.initialize();
});
