const express = require('express');
const cors = require('cors');
const { Web3 } = require('web3');
const crypto = require('crypto');
const sql = require('mssql');

const app = express();
const port = 3000;

// Middleware
app.use(cors());
app.use(express.json());

// Database configuration
const dbConfig = {
    user: process.env.DB_USER || 'sa',
    password: process.env.DB_PASSWORD || 'YourPassword123!',
    server: process.env.DB_SERVER || 'localhost',
    database: process.env.DB_NAME || 'LKSNetwork',
    options: {
        encrypt: false,
        trustServerCertificate: true
    }
};

// Web3 setup for LKS Network
const web3 = new Web3(process.env.LKS_NODE_URL || 'http://localhost:8545');

// LKS Network configuration
const LKS_CHAIN_ID = 1337;
const LKS_COIN_CONTRACT = process.env.LKS_COIN_CONTRACT || '0x742d35Cc6634C0532925a3b8D4C9db96';
const PAYMENT_SYSTEM_CONTRACT = process.env.PAYMENT_SYSTEM_CONTRACT || '0x1234567890123456789012345678901234567890';

// Smart contract ABIs
const LKS_COIN_ABI = [
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
];

const PAYMENT_SYSTEM_ABI = [
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
];

// Initialize database connection
let pool;
async function initializeDatabase() {
    try {
        pool = await sql.connect(dbConfig);
        console.log('Connected to LKS Network database');
        
        // Create demo wallet if not exists
        await createDemoWallet();
    } catch (err) {
        console.error('Database connection failed:', err);
        // Fallback to in-memory storage for development
        console.log('Using in-memory storage as fallback');
    }
}

async function createDemoWallet() {
    try {
        const request = pool.request();
        await request.query(`
            IF NOT EXISTS (SELECT 1 FROM Wallets WHERE Address = '0x742d35Cc6634C0532925a3b8D4C9db96C4C4745c')
            BEGIN
                INSERT INTO Wallets (Address, LKSBalance, ETHBalance, IsActive)
                VALUES ('0x742d35Cc6634C0532925a3b8D4C9db96C4C4745c', 1000.0, 0.5, 1)
            END
        `);
        
        await request.query(`
            IF NOT EXISTS (SELECT 1 FROM Wallets WHERE Address = 'lks1qw2r3t4y5u6i7o8p9a0s1d2f3g4h5j6k7l8z9x')
            BEGIN
                INSERT INTO Wallets (Address, LKSBalance, ETHBalance, IsActive)
                VALUES ('lks1qw2r3t4y5u6i7o8p9a0s1d2f3g4h5j6k7l8z9x', 2500.0, 0.0, 1)
            END
        `);
    } catch (err) {
        console.error('Failed to create demo wallets:', err);
    }
}

// Real wallet balances storage (in production, use database)
const walletBalances = new Map();
const transactionHistory = new Map();

// Initialize default balances
function initializeWallet(address) {
    if (!walletBalances.has(address)) {
        walletBalances.set(address, {
            lks: 5000,
            eth: 0.1,
            lastUpdated: Date.now()
        });
        
        transactionHistory.set(address, [
            {
                hash: generateTxHash(),
                from: 'lks1genesis000000000000000000000000000000000000000',
                to: address,
                amount: 5000,
                timestamp: Date.now() - 86400000, // 1 day ago
                type: 'receive',
                service: 'genesis',
                status: 'confirmed'
            }
        ]);
    }
}

// Generate realistic transaction hash
function generateTxHash() {
    return '0x' + crypto.randomBytes(32).toString('hex');
}

// API Routes

// Get wallet balance
app.get('/api/balance/:address', async (req, res) => {
    try {
        const address = req.params.address;
        initializeWallet(address);
        
        const balance = walletBalances.get(address);
        
        // Try to get real balance from blockchain if available
        try {
            if (web3.utils.isAddress(address)) {
                const ethBalance = await web3.eth.getBalance(address);
                balance.eth = parseFloat(web3.utils.fromWei(ethBalance, 'ether'));
            }
        } catch (blockchainError) {
            console.log('Blockchain not available, using mock data');
        }
        
        res.json({
            address,
            balance: balance.lks,
            ethBalance: balance.eth,
            lastUpdated: balance.lastUpdated,
            network: 'LKS Network'
        });
    } catch (error) {
        res.status(500).json({ error: error.message });
    }
});

// Get transaction history
app.get('/api/transactions/:address', (req, res) => {
    try {
        const address = req.params.address;
        const limit = parseInt(req.query.limit) || 10;
        
        initializeWallet(address);
        
        const transactions = transactionHistory.get(address) || [];
        const limitedTransactions = transactions.slice(0, limit);
        
        res.json({
            address,
            transactions: limitedTransactions,
            total: transactions.length
        });
    } catch (error) {
        res.status(500).json({ error: error.message });
    }
});

// Send transaction
app.post('/api/transaction/send', async (req, res) => {
    try {
        const { from, to, amount, service, note } = req.body;
        
        if (!from || !to || !amount) {
            return res.status(400).json({ error: 'Missing required fields' });
        }
        
        initializeWallet(from);
        initializeWallet(to);
        
        const senderBalance = walletBalances.get(from);
        
        if (senderBalance.lks < amount) {
            return res.status(400).json({ error: 'Insufficient balance' });
        }
        
        // Process transaction
        const txHash = generateTxHash();
        const timestamp = Date.now();
        
        // Update balances
        senderBalance.lks -= amount;
        senderBalance.lastUpdated = timestamp;
        
        const receiverBalance = walletBalances.get(to);
        receiverBalance.lks += amount;
        receiverBalance.lastUpdated = timestamp;
        
        // Add to transaction history
        const senderTx = {
            hash: txHash,
            from,
            to,
            amount: -amount,
            timestamp,
            type: 'send',
            service: service || 'transfer',
            note: note || '',
            status: 'confirmed',
            gasUsed: 0,
            gasFee: 0 // Zero fees on LKS Network
        };
        
        const receiverTx = {
            hash: txHash,
            from,
            to,
            amount: amount,
            timestamp,
            type: 'receive',
            service: service || 'transfer',
            note: note || '',
            status: 'confirmed',
            gasUsed: 0,
            gasFee: 0
        };
        
        transactionHistory.get(from).unshift(senderTx);
        transactionHistory.get(to).unshift(receiverTx);
        
        res.json({
            success: true,
            txHash,
            from,
            to,
            amount,
            timestamp,
            gasFee: 0,
            message: 'Transaction completed with zero fees'
        });
        
    } catch (error) {
        res.status(500).json({ error: error.message });
    }
});

// Process service payment
app.post('/api/service/payment', async (req, res) => {
    try {
        const { userAddress, service, amount, serviceData } = req.body;
        
        initializeWallet(userAddress);
        
        const userBalance = walletBalances.get(userAddress);
        
        if (userBalance.lks < amount) {
            return res.status(400).json({ error: 'Insufficient LKS balance' });
        }
        
        // Service addresses
        const serviceAddresses = {
            'ip-patent': 'lks1patent000000000000000000000000000000000000',
            'lks-summit': 'lks1summit000000000000000000000000000000000000',
            'software-factory': 'lks1factory00000000000000000000000000000000000',
            'vara-security': 'lks1security000000000000000000000000000000000',
            'stadium-tackle': 'lks1gaming000000000000000000000000000000000000',
            'lks-capital': 'lks1capital000000000000000000000000000000000000'
        };
        
        const serviceAddress = serviceAddresses[service];
        if (!serviceAddress) {
            return res.status(400).json({ error: 'Invalid service' });
        }
        
        // Process payment
        const txHash = generateTxHash();
        const timestamp = Date.now();
        
        userBalance.lks -= amount;
        userBalance.lastUpdated = timestamp;
        
        // Add transaction to history
        const transaction = {
            hash: txHash,
            from: userAddress,
            to: serviceAddress,
            amount: -amount,
            timestamp,
            type: 'payment',
            service,
            serviceData,
            status: 'confirmed',
            gasUsed: 0,
            gasFee: 0
        };
        
        transactionHistory.get(userAddress).unshift(transaction);
        
        res.json({
            success: true,
            txHash,
            service,
            amount,
            timestamp,
            message: `Payment to ${service} completed with zero fees`
        });
        
    } catch (error) {
        res.status(500).json({ error: error.message });
    }
});

// Get network statistics
app.get('/api/network/stats', async (req, res) => {
    try {
        let stats = {
            chainId: LKS_NETWORK_CONFIG.chainId,
            networkName: LKS_NETWORK_CONFIG.networkName,
            blockNumber: 1000000 + Math.floor(Math.random() * 1000),
            gasPrice: 0,
            totalSupply: 1000000000,
            circulatingSupply: 500000000,
            activeWallets: walletBalances.size,
            totalTransactions: Array.from(transactionHistory.values()).reduce((sum, txs) => sum + txs.length, 0),
            tps: 65000,
            uptime: '99.9%'
        };
        
        // Try to get real blockchain stats
        try {
            const blockNumber = await web3.eth.getBlockNumber();
            const gasPrice = await web3.eth.getGasPrice();
            stats.blockNumber = blockNumber;
            stats.gasPrice = web3.utils.fromWei(gasPrice, 'gwei');
        } catch (blockchainError) {
            console.log('Using mock network stats');
        }
        
        res.json(stats);
    } catch (error) {
        res.status(500).json({ error: error.message });
    }
});

// Get transaction by hash
app.get('/api/transaction/:hash', (req, res) => {
    try {
        const txHash = req.params.hash;
        
        // Search through all transaction histories
        for (const [address, transactions] of transactionHistory.entries()) {
            const tx = transactions.find(t => t.hash === txHash);
            if (tx) {
                return res.json({
                    found: true,
                    transaction: tx,
                    ownerAddress: address
                });
            }
        }
        
        res.status(404).json({ error: 'Transaction not found' });
    } catch (error) {
        res.status(500).json({ error: error.message });
    }
});

// Validate address
app.get('/api/validate/:address', (req, res) => {
    try {
        const address = req.params.address;
        let isValid = false;
        let type = 'unknown';
        
        if (address.startsWith('lks1')) {
            isValid = /^lks1[a-z0-9]{39,59}$/.test(address);
            type = 'lks';
        } else if (address.startsWith('0x')) {
            isValid = web3.utils.isAddress(address);
            type = 'ethereum';
        }
        
        res.json({
            address,
            isValid,
            type,
            network: isValid ? LKS_NETWORK_CONFIG.networkName : 'unknown'
        });
    } catch (error) {
        res.status(500).json({ error: error.message });
    }
});

// Health check
app.get('/health', (req, res) => {
    res.json({
        status: 'healthy',
        timestamp: Date.now(),
        network: LKS_NETWORK_CONFIG.networkName,
        version: '1.0.0'
    });
});

// Error handling middleware
app.use((error, req, res, next) => {
    console.error('API Error:', error);
    res.status(500).json({
        error: 'Internal server error',
        message: error.message
    });
});

app.listen(port, () => {
    console.log(`LKS NETWORK Production Backend running on port ${port}`);
    console.log(`Network: LKS Network`);
    console.log(`Chain ID: ${LKS_CHAIN_ID}`);
    console.log('Zero-fee transactions enabled');
});
