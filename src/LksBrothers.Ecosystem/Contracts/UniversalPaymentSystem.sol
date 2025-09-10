// SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

import "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import "@openzeppelin/contracts/access/Ownable.sol";
import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "@openzeppelin/contracts/utils/Pausable.sol";

/**
 * @title Universal Payment System for LKS Brothers Ecosystem
 * @dev Handles all payments across LKS Brothers services using LKS COIN
 * Services: IP Patent, LKS Summit, Software Factory, Vara Security, Stadium Tackle, LKS Capital
 */
contract UniversalPaymentSystem is Ownable, ReentrancyGuard, Pausable {
    IERC20 public immutable lksCoin;
    
    // Service identifiers
    enum ServiceType {
        IP_PATENT,
        LKS_SUMMIT,
        SOFTWARE_FACTORY,
        VARA_SECURITY,
        STADIUM_TACKLE,
        LKS_CAPITAL
    }
    
    // Service configuration
    struct ServiceConfig {
        address serviceProvider;
        bool isActive;
        uint256 minPayment;
        uint256 maxPayment;
        string serviceName;
    }
    
    // Payment record
    struct Payment {
        address user;
        ServiceType serviceType;
        uint256 amount;
        uint256 timestamp;
        string referenceId;
        bool isProcessed;
    }
    
    // Subscription record
    struct Subscription {
        address user;
        ServiceType serviceType;
        uint256 monthlyAmount;
        uint256 startDate;
        uint256 endDate;
        bool isActive;
        uint256 lastPayment;
    }
    
    // State variables
    mapping(ServiceType => ServiceConfig) public services;
    mapping(address => mapping(ServiceType => uint256)) public userBalances;
    mapping(uint256 => Payment) public payments;
    mapping(uint256 => Subscription) public subscriptions;
    mapping(address => uint256[]) public userPayments;
    mapping(address => uint256[]) public userSubscriptions;
    
    uint256 public nextPaymentId = 1;
    uint256 public nextSubscriptionId = 1;
    uint256 public totalVolume;
    uint256 public burnPercentage = 500; // 5% burn rate (basis points)
    
    // Events
    event PaymentProcessed(
        uint256 indexed paymentId,
        address indexed user,
        ServiceType indexed serviceType,
        uint256 amount,
        string referenceId
    );
    
    event SubscriptionCreated(
        uint256 indexed subscriptionId,
        address indexed user,
        ServiceType indexed serviceType,
        uint256 monthlyAmount,
        uint256 duration
    );
    
    event SubscriptionPayment(
        uint256 indexed subscriptionId,
        address indexed user,
        uint256 amount,
        uint256 timestamp
    );
    
    event ServiceConfigured(
        ServiceType indexed serviceType,
        address serviceProvider,
        string serviceName
    );
    
    event TokensBurned(uint256 amount);
    
    constructor(address _lksCoin) Ownable(msg.sender) {
        lksCoin = IERC20(_lksCoin);
        
        // Initialize service configurations
        _initializeServices();
    }
    
    /**
     * @dev Initialize all LKS Brothers services
     */
    function _initializeServices() private {
        // IP Patent Service
        services[ServiceType.IP_PATENT] = ServiceConfig({
            serviceProvider: address(0),
            isActive: true,
            minPayment: 100 * 10**18, // 100 LKS minimum
            maxPayment: 10000 * 10**18, // 10,000 LKS maximum
            serviceName: "IP Patent Services"
        });
        
        // LKS Summit Events
        services[ServiceType.LKS_SUMMIT] = ServiceConfig({
            serviceProvider: address(0),
            isActive: true,
            minPayment: 10 * 10**18, // 10 LKS minimum
            maxPayment: 1000 * 10**18, // 1,000 LKS maximum
            serviceName: "LKS Summit Events"
        });
        
        // Software Factory
        services[ServiceType.SOFTWARE_FACTORY] = ServiceConfig({
            serviceProvider: address(0),
            isActive: true,
            minPayment: 500 * 10**18, // 500 LKS minimum
            maxPayment: 100000 * 10**18, // 100,000 LKS maximum
            serviceName: "Software Factory"
        });
        
        // Vara Cybersecurity
        services[ServiceType.VARA_SECURITY] = ServiceConfig({
            serviceProvider: address(0),
            isActive: true,
            minPayment: 200 * 10**18, // 200 LKS minimum
            maxPayment: 50000 * 10**18, // 50,000 LKS maximum
            serviceName: "Vara Cybersecurity"
        });
        
        // Stadium Tackle Gaming
        services[ServiceType.STADIUM_TACKLE] = ServiceConfig({
            serviceProvider: address(0),
            isActive: true,
            minPayment: 1 * 10**18, // 1 LKS minimum
            maxPayment: 10000 * 10**18, // 10,000 LKS maximum
            serviceName: "Stadium Tackle Gaming"
        });
        
        // LKS Capital Crowdfunding
        services[ServiceType.LKS_CAPITAL] = ServiceConfig({
            serviceProvider: address(0),
            isActive: true,
            minPayment: 10 * 10**18, // 10 LKS minimum
            maxPayment: 1000000 * 10**18, // 1,000,000 LKS maximum
            serviceName: "LKS Capital Crowdfunding"
        });
    }
    
    /**
     * @dev Process a one-time payment for any LKS Brothers service
     * @param serviceType The service being paid for
     * @param amount Amount of LKS COIN to pay
     * @param referenceId Service-specific reference ID
     */
    function payForService(
        ServiceType serviceType,
        uint256 amount,
        string memory referenceId
    ) external nonReentrant whenNotPaused returns (uint256 paymentId) {
        require(services[serviceType].isActive, "Service not active");
        require(amount >= services[serviceType].minPayment, "Amount below minimum");
        require(amount <= services[serviceType].maxPayment, "Amount above maximum");
        require(bytes(referenceId).length > 0, "Reference ID required");
        
        // Transfer LKS COIN from user
        require(
            lksCoin.transferFrom(msg.sender, address(this), amount),
            "Transfer failed"
        );
        
        // Calculate burn amount
        uint256 burnAmount = (amount * burnPercentage) / 10000;
        uint256 serviceAmount = amount - burnAmount;
        
        // Burn tokens by sending to dead address
        if (burnAmount > 0) {
            require(
                lksCoin.transfer(address(0xdead), burnAmount),
                "Burn failed"
            );
            emit TokensBurned(burnAmount);
        }
        
        // Transfer to service provider (if set) or keep in contract
        address serviceProvider = services[serviceType].serviceProvider;
        if (serviceProvider != address(0)) {
            require(
                lksCoin.transfer(serviceProvider, serviceAmount),
                "Service transfer failed"
            );
        } else {
            userBalances[msg.sender][serviceType] += serviceAmount;
        }
        
        // Record payment
        paymentId = nextPaymentId++;
        payments[paymentId] = Payment({
            user: msg.sender,
            serviceType: serviceType,
            amount: amount,
            timestamp: block.timestamp,
            referenceId: referenceId,
            isProcessed: true
        });
        
        userPayments[msg.sender].push(paymentId);
        totalVolume += amount;
        
        emit PaymentProcessed(paymentId, msg.sender, serviceType, amount, referenceId);
        
        return paymentId;
    }
    
    /**
     * @dev Create a subscription for recurring payments
     * @param serviceType The service to subscribe to
     * @param monthlyAmount Monthly payment amount in LKS COIN
     * @param durationMonths Subscription duration in months
     */
    function createSubscription(
        ServiceType serviceType,
        uint256 monthlyAmount,
        uint256 durationMonths
    ) external nonReentrant whenNotPaused returns (uint256 subscriptionId) {
        require(services[serviceType].isActive, "Service not active");
        require(monthlyAmount >= services[serviceType].minPayment, "Amount below minimum");
        require(durationMonths > 0 && durationMonths <= 120, "Invalid duration"); // Max 10 years
        
        // Process first payment
        require(
            lksCoin.transferFrom(msg.sender, address(this), monthlyAmount),
            "Initial payment failed"
        );
        
        // Calculate subscription end date
        uint256 endDate = block.timestamp + (durationMonths * 30 days);
        
        // Create subscription record
        subscriptionId = nextSubscriptionId++;
        subscriptions[subscriptionId] = Subscription({
            user: msg.sender,
            serviceType: serviceType,
            monthlyAmount: monthlyAmount,
            startDate: block.timestamp,
            endDate: endDate,
            isActive: true,
            lastPayment: block.timestamp
        });
        
        userSubscriptions[msg.sender].push(subscriptionId);
        
        emit SubscriptionCreated(subscriptionId, msg.sender, serviceType, monthlyAmount, durationMonths);
        emit SubscriptionPayment(subscriptionId, msg.sender, monthlyAmount, block.timestamp);
        
        return subscriptionId;
    }
    
    /**
     * @dev Process subscription payment (called monthly)
     * @param subscriptionId The subscription to process
     */
    function processSubscriptionPayment(uint256 subscriptionId) external nonReentrant {
        Subscription storage sub = subscriptions[subscriptionId];
        require(sub.isActive, "Subscription not active");
        require(block.timestamp >= sub.lastPayment + 30 days, "Payment not due");
        require(block.timestamp <= sub.endDate, "Subscription expired");
        
        // Process payment
        require(
            lksCoin.transferFrom(sub.user, address(this), sub.monthlyAmount),
            "Subscription payment failed"
        );
        
        sub.lastPayment = block.timestamp;
        
        emit SubscriptionPayment(subscriptionId, sub.user, sub.monthlyAmount, block.timestamp);
    }
    
    /**
     * @dev Cancel an active subscription
     * @param subscriptionId The subscription to cancel
     */
    function cancelSubscription(uint256 subscriptionId) external {
        Subscription storage sub = subscriptions[subscriptionId];
        require(sub.user == msg.sender, "Not subscription owner");
        require(sub.isActive, "Subscription not active");
        
        sub.isActive = false;
    }
    
    /**
     * @dev Configure a service (admin only)
     * @param serviceType The service to configure
     * @param serviceProvider Address to receive payments
     * @param minPayment Minimum payment amount
     * @param maxPayment Maximum payment amount
     * @param isActive Whether service is active
     */
    function configureService(
        ServiceType serviceType,
        address serviceProvider,
        uint256 minPayment,
        uint256 maxPayment,
        bool isActive
    ) external onlyOwner {
        services[serviceType].serviceProvider = serviceProvider;
        services[serviceType].minPayment = minPayment;
        services[serviceType].maxPayment = maxPayment;
        services[serviceType].isActive = isActive;
        
        emit ServiceConfigured(serviceType, serviceProvider, services[serviceType].serviceName);
    }
    
    /**
     * @dev Update burn percentage (admin only)
     * @param newBurnPercentage New burn percentage in basis points
     */
    function setBurnPercentage(uint256 newBurnPercentage) external onlyOwner {
        require(newBurnPercentage <= 1000, "Burn percentage too high"); // Max 10%
        burnPercentage = newBurnPercentage;
    }
    
    /**
     * @dev Get user's payment history
     * @param user User address
     * @return Array of payment IDs
     */
    function getUserPayments(address user) external view returns (uint256[] memory) {
        return userPayments[user];
    }
    
    /**
     * @dev Get user's subscription history
     * @param user User address
     * @return Array of subscription IDs
     */
    function getUserSubscriptions(address user) external view returns (uint256[] memory) {
        return userSubscriptions[user];
    }
    
    /**
     * @dev Get service statistics
     * @param serviceType The service to query
     * @return Service configuration and stats
     */
    function getServiceInfo(ServiceType serviceType) external view returns (
        string memory serviceName,
        address serviceProvider,
        bool isActive,
        uint256 minPayment,
        uint256 maxPayment
    ) {
        ServiceConfig memory config = services[serviceType];
        return (
            config.serviceName,
            config.serviceProvider,
            config.isActive,
            config.minPayment,
            config.maxPayment
        );
    }
    
    /**
     * @dev Emergency pause (admin only)
     */
    function pause() external onlyOwner {
        _pause();
    }
    
    /**
     * @dev Unpause (admin only)
     */
    function unpause() external onlyOwner {
        _unpause();
    }
    
    /**
     * @dev Emergency token recovery (admin only)
     * @param token Token address to recover
     * @param amount Amount to recover
     */
    function emergencyRecovery(address token, uint256 amount) external onlyOwner {
        IERC20(token).transfer(owner(), amount);
    }
}
