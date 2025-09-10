class LKSNetworkAPI {
    constructor() {
        this.baseURL = 'http://localhost:8545'; // LKS Network RPC endpoint
        this.explorerAPI = 'http://localhost:3000/api';
        this.chainId = 1337; // LKS Network Chain ID
        this.init();
    }

    init() {
        this.setupWeb3Provider();
        this.initializeContracts();
    }

    // Setup Web3 Provider for LKS Network
    setupWeb3Provider() {
        if (typeof window.ethereum !== 'undefined') {
            this.web3 = new Web3(window.ethereum);
        } else {
            // Fallback to HTTP provider
            this.web3 = new Web3(new Web3.providers.HttpProvider(this.baseURL));
        }
    }

    // Initialize Smart Contracts
    initializeContracts() {
        // LKS COIN Token Contract
        this.lksCoinContract = {
            address: '0x742d35Cc6634C0532925a3b8D4C9db96',
            abi: [
                {
                    "constant": true,
                    "inputs": [{"name": "_owner", "type": "address"}],
                    "name": "balanceOf",
                    "outputs": [{"name": "balance", "type": "uint256"}],
                    "type": "function"
                },
                {
                    "constant": false,
                    "inputs": [
                        {"name": "_to", "type": "address"},
                        {"name": "_value", "type": "uint256"}
                    ],
                    "name": "transfer",
                    "outputs": [{"name": "", "type": "bool"}],
                    "type": "function"
                }
            ]
        };

        // Universal Payment System Contract
        this.paymentContract = {
            address: '0x1234567890123456789012345678901234567890',
            abi: [
                {
                    "constant": false,
                    "inputs": [
                        {"name": "_service", "type": "uint8"},
                        {"name": "_amount", "type": "uint256"},
                        {"name": "_recipient", "type": "address"}
                    ],
                    "name": "processPayment",
                    "outputs": [{"name": "", "type": "bool"}],
                    "type": "function"
                }
            ]
        };
    }

    // Get real LKS COIN balance
    async getLKSBalance(address) {
        try {
            const contract = new this.web3.eth.Contract(
                this.lksCoinContract.abi, 
                this.lksCoinContract.address
            );
            
            const balance = await contract.methods.balanceOf(address).call();
            return this.web3.utils.fromWei(balance, 'ether');
        } catch (error) {
            console.error('Error getting LKS balance:', error);
            // Fallback to API call
            return this.getLKSBalanceFromAPI(address);
        }
    }

    // Fallback API call for balance
    async getLKSBalanceFromAPI(address) {
        try {
            const response = await fetch(`${this.explorerAPI}/balance/${address}`);
            const data = await response.json();
            return data.balance || 0;
        } catch (error) {
            console.error('API balance fetch failed:', error);
            return 0;
        }
    }

    // Send LKS COIN transaction
    async sendLKSTransaction(from, to, amount, privateKey = null) {
        try {
            const contract = new this.web3.eth.Contract(
                this.lksCoinContract.abi,
                this.lksCoinContract.address
            );

            const amountWei = this.web3.utils.toWei(amount.toString(), 'ether');
            
            // Get transaction count for nonce
            const nonce = await this.web3.eth.getTransactionCount(from, 'latest');
            
            // Build transaction
            const transaction = {
                from: from,
                to: this.lksCoinContract.address,
                data: contract.methods.transfer(to, amountWei).encodeABI(),
                gas: 21000,
                gasPrice: 0, // Zero fees on LKS Network
                nonce: nonce
            };

            // Sign and send transaction
            if (window.ethereum) {
                const txHash = await window.ethereum.request({
                    method: 'eth_sendTransaction',
                    params: [transaction]
                });
                return txHash;
            } else {
                throw new Error('No wallet connected');
            }
        } catch (error) {
            console.error('Transaction failed:', error);
            throw error;
        }
    }

    // Process payment through Universal Payment System
    async processServicePayment(service, amount, recipient, userAddress) {
        try {
            const contract = new this.web3.eth.Contract(
                this.paymentContract.abi,
                this.paymentContract.address
            );

            const amountWei = this.web3.utils.toWei(amount.toString(), 'ether');
            
            const transaction = {
                from: userAddress,
                to: this.paymentContract.address,
                data: contract.methods.processPayment(service, amountWei, recipient).encodeABI(),
                gas: 50000,
                gasPrice: 0 // Zero fees
            };

            const txHash = await window.ethereum.request({
                method: 'eth_sendTransaction',
                params: [transaction]
            });

            return txHash;
        } catch (error) {
            console.error('Service payment failed:', error);
            throw error;
        }
    }

    // Get transaction details
    async getTransaction(txHash) {
        try {
            const tx = await this.web3.eth.getTransaction(txHash);
            const receipt = await this.web3.eth.getTransactionReceipt(txHash);
            
            return {
                hash: tx.hash,
                from: tx.from,
                to: tx.to,
                value: this.web3.utils.fromWei(tx.value, 'ether'),
                gasUsed: receipt.gasUsed,
                status: receipt.status,
                blockNumber: receipt.blockNumber,
                timestamp: await this.getBlockTimestamp(receipt.blockNumber)
            };
        } catch (error) {
            console.error('Error getting transaction:', error);
            return null;
        }
    }

    // Get block timestamp
    async getBlockTimestamp(blockNumber) {
        try {
            const block = await this.web3.eth.getBlock(blockNumber);
            return block.timestamp;
        } catch (error) {
            return Date.now() / 1000;
        }
    }

    // Get network stats
    async getNetworkStats() {
        try {
            const [blockNumber, gasPrice, peerCount] = await Promise.all([
                this.web3.eth.getBlockNumber(),
                this.web3.eth.getGasPrice(),
                this.web3.eth.net.getPeerCount()
            ]);

            return {
                blockNumber,
                gasPrice: this.web3.utils.fromWei(gasPrice, 'gwei'),
                peerCount,
                chainId: await this.web3.eth.getChainId()
            };
        } catch (error) {
            console.error('Error getting network stats:', error);
            return {
                blockNumber: 0,
                gasPrice: 0,
                peerCount: 0,
                chainId: this.chainId
            };
        }
    }

    // Validate address format
    isValidAddress(address) {
        if (address.startsWith('lks1')) {
            // LKS Network native address format
            return /^lks1[a-z0-9]{39,59}$/.test(address);
        } else {
            // Ethereum-compatible address
            return this.web3.utils.isAddress(address);
        }
    }

    // Convert LKS address to Ethereum format
    lksToEthAddress(lksAddress) {
        if (lksAddress.startsWith('lks1')) {
            // Convert LKS bech32 to Ethereum hex format
            // This is a simplified conversion - real implementation would use bech32 library
            const hash = this.web3.utils.keccak256(lksAddress);
            return '0x' + hash.slice(-40);
        }
        return lksAddress;
    }

    // Get transaction history
    async getTransactionHistory(address, limit = 10) {
        try {
            const response = await fetch(`${this.explorerAPI}/transactions/${address}?limit=${limit}`);
            const data = await response.json();
            return data.transactions || [];
        } catch (error) {
            console.error('Error getting transaction history:', error);
            return [];
        }
    }

    // Estimate gas for transaction
    async estimateGas(transaction) {
        try {
            const gasEstimate = await this.web3.eth.estimateGas(transaction);
            return gasEstimate;
        } catch (error) {
            console.error('Gas estimation failed:', error);
            return 21000; // Default gas limit
        }
    }

    // Check if connected to LKS Network
    async isConnectedToLKSNetwork() {
        try {
            const chainId = await this.web3.eth.getChainId();
            return chainId === this.chainId;
        } catch (error) {
            return false;
        }
    }

    // Add LKS Network to MetaMask
    async addLKSNetworkToMetaMask() {
        if (!window.ethereum) return false;

        try {
            await window.ethereum.request({
                method: 'wallet_addEthereumChain',
                params: [{
                    chainId: `0x${this.chainId.toString(16)}`,
                    chainName: 'LKS Network',
                    nativeCurrency: {
                        name: 'LKS COIN',
                        symbol: 'LKS',
                        decimals: 18
                    },
                    rpcUrls: [this.baseURL],
                    blockExplorerUrls: ['http://localhost:8080']
                }]
            });
            return true;
        } catch (error) {
            console.error('Failed to add LKS Network:', error);
            return false;
        }
    }

    // Monitor pending transactions
    monitorTransaction(txHash, callback) {
        const checkTransaction = async () => {
            try {
                const receipt = await this.web3.eth.getTransactionReceipt(txHash);
                if (receipt) {
                    callback(null, receipt);
                } else {
                    setTimeout(checkTransaction, 2000); // Check every 2 seconds
                }
            } catch (error) {
                callback(error, null);
            }
        };

        checkTransaction();
    }

    // Get service-specific payment details
    getServicePaymentDetails(serviceId) {
        const services = {
            0: { name: 'IP Patent', fee: 100, address: '0x1111...' },
            1: { name: 'LKS Summit', fee: 50, address: '0x2222...' },
            2: { name: 'Software Factory', fee: 200, address: '0x3333...' },
            3: { name: 'Vara Security', fee: 150, address: '0x4444...' },
            4: { name: 'Stadium Tackle', fee: 25, address: '0x5555...' },
            5: { name: 'LKS Capital', fee: 75, address: '0x6666...' }
        };

        return services[serviceId] || null;
    }
}

// Initialize LKS Network API
const lksAPI = new LKSNetworkAPI();
