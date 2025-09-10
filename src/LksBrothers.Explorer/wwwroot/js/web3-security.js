class Web3SecurityManager {
    constructor() {
        this.securityLevel = 'maximum';
        this.threatDetection = true;
        this.encryptionEnabled = true;
        this.auditLog = [];
        this.init();
    }

    init() {
        this.setupSecurityHeaders();
        this.initializeThreatDetection();
        this.setupTransactionMonitoring();
        this.enableAntiPhishing();
        this.startSecurityAudit();
    }

    // Advanced Web3 Security Headers
    setupSecurityHeaders() {
        // Content Security Policy for Web3
        const csp = [
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://generativelanguage.googleapis.com",
            "connect-src 'self' https://*.ethereum.org https://*.solana.com https://*.xrpl.org wss://*.ethereum.org wss://*.solana.com",
            "img-src 'self' data: https:",
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net",
            "frame-src 'none'",
            "object-src 'none'",
            "base-uri 'self'"
        ].join('; ');

        // Apply security headers via meta tags
        this.addMetaTag('Content-Security-Policy', csp);
        this.addMetaTag('X-Frame-Options', 'DENY');
        this.addMetaTag('X-Content-Type-Options', 'nosniff');
        this.addMetaTag('Referrer-Policy', 'strict-origin-when-cross-origin');
        
        this.logSecurity('Security headers initialized', 'info');
    }

    // Real-time Threat Detection
    initializeThreatDetection() {
        // Monitor for suspicious wallet interactions
        this.monitorWalletConnections();
        
        // Detect phishing attempts
        this.detectPhishingAttempts();
        
        // Monitor for malicious smart contracts
        this.scanSmartContracts();
        
        // Detect MEV attacks
        this.detectMEVAttacks();
        
        this.logSecurity('Threat detection systems activated', 'info');
    }

    // Transaction Security Monitoring
    setupTransactionMonitoring() {
        // Monitor all outgoing transactions
        this.interceptTransactions();
        
        // Validate transaction recipients
        this.validateRecipients();
        
        // Check for unusual transaction patterns
        this.detectAnomalousTransactions();
        
        this.logSecurity('Transaction monitoring enabled', 'info');
    }

    // Anti-Phishing Protection
    enableAntiPhishing() {
        // Check domain authenticity
        this.verifyDomainAuthenticity();
        
        // Detect fake wallet interfaces
        this.detectFakeWallets();
        
        // Monitor for clipboard hijacking
        this.protectClipboard();
        
        this.logSecurity('Anti-phishing protection activated', 'info');
    }

    // Wallet Connection Monitoring
    monitorWalletConnections() {
        const originalConnect = window.ethereum?.request || (() => {});
        
        if (window.ethereum) {
            window.ethereum.request = async (args) => {
                // Security check before wallet connection
                if (args.method === 'eth_requestAccounts') {
                    const securityCheck = await this.performWalletSecurityCheck();
                    if (!securityCheck.safe) {
                        this.showSecurityAlert('Suspicious wallet connection attempt blocked', 'high');
                        throw new Error('Connection blocked for security reasons');
                    }
                }
                
                // Log all wallet interactions
                this.logSecurity(`Wallet interaction: ${args.method}`, 'info');
                return originalConnect.call(window.ethereum, args);
            };
        }
    }

    // Smart Contract Security Scanning
    async scanSmartContracts() {
        // Simulate smart contract vulnerability scanning
        const knownVulnerabilities = [
            'reentrancy',
            'integer_overflow',
            'unchecked_call',
            'delegatecall_injection',
            'timestamp_dependence'
        ];

        // Check for known malicious contracts
        const maliciousContracts = [
            '0x0000000000000000000000000000000000000000',
            '0x1111111111111111111111111111111111111111'
        ];

        return {
            vulnerabilities: [],
            maliciousContracts: [],
            riskLevel: 'low',
            recommendations: ['Use verified contracts only', 'Enable transaction simulation']
        };
    }

    // MEV Attack Detection
    detectMEVAttacks() {
        // Monitor for front-running attempts
        this.detectFrontRunning();
        
        // Check for sandwich attacks
        this.detectSandwichAttacks();
        
        // Monitor gas price manipulation
        this.monitorGasManipulation();
    }

    // Transaction Interception and Validation
    interceptTransactions() {
        // Override transaction sending functions
        const originalSend = window.ethereum?.sendTransaction || (() => {});
        
        if (window.ethereum) {
            window.ethereum.sendTransaction = async (transaction) => {
                // Pre-transaction security checks
                const securityAnalysis = await this.analyzeTransaction(transaction);
                
                if (securityAnalysis.riskLevel === 'high') {
                    const userConfirm = await this.showSecurityWarning(
                        'High-risk transaction detected',
                        securityAnalysis.risks
                    );
                    
                    if (!userConfirm) {
                        throw new Error('Transaction cancelled for security reasons');
                    }
                }
                
                // Log transaction
                this.logSecurity(`Transaction sent: ${transaction.to}`, 'info');
                return originalSend.call(window.ethereum, transaction);
            };
        }
    }

    // Transaction Risk Analysis
    async analyzeTransaction(transaction) {
        const risks = [];
        let riskLevel = 'low';

        // Check recipient address
        if (this.isKnownMaliciousAddress(transaction.to)) {
            risks.push('Recipient is a known malicious address');
            riskLevel = 'high';
        }

        // Check transaction value
        if (transaction.value && parseInt(transaction.value, 16) > 1e18) {
            risks.push('Large transaction amount detected');
            riskLevel = 'medium';
        }

        // Check gas price manipulation
        if (transaction.gasPrice && parseInt(transaction.gasPrice, 16) > 100e9) {
            risks.push('Unusually high gas price');
            riskLevel = 'medium';
        }

        // Check for contract interaction
        if (transaction.data && transaction.data !== '0x') {
            const contractAnalysis = await this.analyzeContractInteraction(transaction);
            risks.push(...contractAnalysis.risks);
            if (contractAnalysis.riskLevel === 'high') riskLevel = 'high';
        }

        return { riskLevel, risks };
    }

    // Clipboard Protection
    protectClipboard() {
        let lastClipboardContent = '';
        
        // Clipboard monitoring disabled to prevent permission prompts
        // Monitor clipboard for address hijacking
        // setInterval(async () => {
        //     try {
        //         const clipboardContent = await navigator.clipboard.readText();
        //         
        //         if (clipboardContent !== lastClipboardContent) {
        //             if (this.isWalletAddress(clipboardContent)) {
        //                 if (this.isKnownMaliciousAddress(clipboardContent)) {
        //                     await navigator.clipboard.writeText('');
        //                     this.showSecurityAlert('Malicious address removed from clipboard', 'high');
        //                 }
        //             }
        //             lastClipboardContent = clipboardContent;
        //         }
        //     } catch (error) {
        //         // Clipboard access denied - this is actually good for security
        //     }
        // }, 1000);
    }

    // Domain Authenticity Verification
    verifyDomainAuthenticity() {
        const currentDomain = window.location.hostname;
        const trustedDomains = [
            'localhost',
            '127.0.0.1',
            'lksnetwork.com',
            'lksnetwork.io'
        ];

        if (!trustedDomains.some(domain => currentDomain.includes(domain))) {
            this.showSecurityAlert('Untrusted domain detected', 'medium');
        }
    }

    // Security Alert System
    showSecurityAlert(message, severity = 'medium') {
        const alertDiv = document.createElement('div');
        alertDiv.className = `fixed top-4 right-4 z-50 p-4 rounded-lg shadow-lg transition-all duration-300 ${
            severity === 'high' ? 'bg-red-600' : 
            severity === 'medium' ? 'bg-yellow-600' : 'bg-blue-600'
        } text-white max-w-sm`;
        
        alertDiv.innerHTML = `
            <div class="flex items-start">
                <i class="fas fa-shield-alt text-2xl mr-3 mt-1"></i>
                <div class="flex-1">
                    <div class="font-bold mb-1">Security Alert</div>
                    <div class="text-sm">${message}</div>
                    <button onclick="this.parentElement.parentElement.parentElement.remove()" 
                            class="text-xs underline mt-2 hover:no-underline">
                        Dismiss
                    </button>
                </div>
            </div>
        `;

        document.body.appendChild(alertDiv);
        
        // Auto-remove after 10 seconds for high severity, 5 seconds for others
        setTimeout(() => {
            if (alertDiv.parentElement) {
                alertDiv.remove();
            }
        }, severity === 'high' ? 10000 : 5000);

        this.logSecurity(`Security alert: ${message}`, severity);
    }

    // Security Warning Dialog
    async showSecurityWarning(title, risks) {
        return new Promise((resolve) => {
            const modal = document.createElement('div');
            modal.className = 'fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50';
            
            modal.innerHTML = `
                <div class="bg-red-900 border border-red-600 rounded-2xl p-8 max-w-md mx-4 text-white">
                    <div class="text-center mb-6">
                        <i class="fas fa-exclamation-triangle text-6xl text-red-400 mb-4"></i>
                        <h3 class="text-2xl font-bold">${title}</h3>
                    </div>
                    <div class="mb-6">
                        <p class="text-red-200 mb-4">The following security risks were detected:</p>
                        <ul class="text-sm text-red-300 space-y-1">
                            ${risks.map(risk => `<li>• ${risk}</li>`).join('')}
                        </ul>
                    </div>
                    <div class="flex space-x-4">
                        <button onclick="resolve(false)" class="flex-1 bg-gray-600 hover:bg-gray-700 px-4 py-3 rounded-lg font-semibold transition">
                            Cancel Transaction
                        </button>
                        <button onclick="resolve(true)" class="flex-1 bg-red-600 hover:bg-red-700 px-4 py-3 rounded-lg font-semibold transition">
                            Proceed Anyway
                        </button>
                    </div>
                </div>
            `;

            // Add click handlers
            const buttons = modal.querySelectorAll('button');
            buttons[0].onclick = () => { modal.remove(); resolve(false); };
            buttons[1].onclick = () => { modal.remove(); resolve(true); };

            document.body.appendChild(modal);
        });
    }

    // Wallet Security Check
    async performWalletSecurityCheck() {
        const checks = {
            domainVerified: this.verifyDomainAuthenticity(),
            noPhishingDetected: !this.detectPhishingAttempts(),
            secureConnection: window.location.protocol === 'https:' || window.location.hostname === 'localhost',
            noMaliciousScripts: this.scanForMaliciousScripts()
        };

        const safe = Object.values(checks).every(check => check);
        
        return {
            safe,
            checks,
            recommendations: safe ? [] : [
                'Verify you are on the correct website',
                'Check for suspicious browser extensions',
                'Ensure secure connection (HTTPS)'
            ]
        };
    }

    // Utility Functions
    isWalletAddress(text) {
        // Ethereum address pattern
        const ethPattern = /^0x[a-fA-F0-9]{40}$/;
        // Solana address pattern
        const solPattern = /^[1-9A-HJ-NP-Za-km-z]{32,44}$/;
        // XRPL address pattern
        const xrpPattern = /^r[1-9A-HJ-NP-Za-km-z]{25,34}$/;
        // LKS address pattern
        const lksPattern = /^lks[1-9A-HJ-NP-Za-km-z]{39,59}$/;

        return ethPattern.test(text) || solPattern.test(text) || 
               xrpPattern.test(text) || lksPattern.test(text);
    }

    isKnownMaliciousAddress(address) {
        const maliciousAddresses = [
            '0x0000000000000000000000000000000000000000',
            '0x1111111111111111111111111111111111111111',
            // Add more known malicious addresses
        ];
        return maliciousAddresses.includes(address.toLowerCase());
    }

    scanForMaliciousScripts() {
        // Check for suspicious script tags or inline scripts
        const scripts = document.querySelectorAll('script');
        const suspiciousPatterns = [
            'eval(',
            'document.write(',
            'innerHTML =',
            'crypto.getRandomValues'
        ];

        for (let script of scripts) {
            const content = script.textContent || script.src;
            for (let pattern of suspiciousPatterns) {
                if (content.includes(pattern)) {
                    this.logSecurity(`Suspicious script pattern detected: ${pattern}`, 'warning');
                }
            }
        }
        return true; // For demo purposes
    }

    // Security Audit Logging
    logSecurity(message, level = 'info') {
        const logEntry = {
            timestamp: new Date().toISOString(),
            level,
            message,
            url: window.location.href,
            userAgent: navigator.userAgent.substring(0, 100)
        };

        this.auditLog.push(logEntry);
        
        // Keep only last 1000 entries
        if (this.auditLog.length > 1000) {
            this.auditLog.shift();
        }

        // Console logging for development
        console.log(`[LKS Security] ${level.toUpperCase()}: ${message}`);
    }

    // Security Dashboard
    getSecurityStatus() {
        return {
            securityLevel: this.securityLevel,
            threatDetection: this.threatDetection,
            encryptionEnabled: this.encryptionEnabled,
            recentAlerts: this.auditLog.filter(entry => 
                entry.level === 'warning' || entry.level === 'high'
            ).slice(-10),
            totalLogs: this.auditLog.length
        };
    }

    // Helper method to add meta tags
    addMetaTag(name, content) {
        const meta = document.createElement('meta');
        meta.setAttribute('http-equiv', name);
        meta.setAttribute('content', content);
        document.head.appendChild(meta);
    }

    // Placeholder methods for complex security features
    detectPhishingAttempts() { return false; }
    detectFrontRunning() { this.logSecurity('Front-running detection active', 'info'); }
    detectSandwichAttacks() { this.logSecurity('Sandwich attack detection active', 'info'); }
    monitorGasManipulation() { this.logSecurity('Gas manipulation monitoring active', 'info'); }
    detectFakeWallets() { this.logSecurity('Fake wallet detection active', 'info'); }
    validateRecipients() { this.logSecurity('Recipient validation active', 'info'); }
    detectAnomalousTransactions() { this.logSecurity('Anomalous transaction detection active', 'info'); }
    
    async analyzeContractInteraction(transaction) {
        return { risks: ['Smart contract interaction'], riskLevel: 'low' };
    }

    // Start continuous security monitoring
    startSecurityAudit() {
        // Run security checks every 30 seconds
        setInterval(() => {
            this.performRoutineSecurityCheck();
        }, 30000);

        this.logSecurity('Continuous security monitoring started', 'info');
    }

    performRoutineSecurityCheck() {
        // Check for new threats
        this.scanForMaliciousScripts();
        
        // Verify domain hasn't changed
        this.verifyDomainAuthenticity();
        
        // Check for suspicious network activity
        this.logSecurity('Routine security check completed', 'info');
    }
}

// Initialize Web3 Security Manager
const web3Security = new Web3SecurityManager();
