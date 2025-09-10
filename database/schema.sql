-- LKS Network Production Database Schema

-- Create database
CREATE DATABASE LKSNetwork;
USE LKSNetwork;

-- Wallets table
CREATE TABLE Wallets (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Address NVARCHAR(100) NOT NULL UNIQUE,
    LKSBalance DECIMAL(38,18) NOT NULL DEFAULT 0,
    ETHBalance DECIMAL(38,18) NOT NULL DEFAULT 0,
    LastUpdated DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Wallets_Address (Address),
    INDEX IX_Wallets_LastUpdated (LastUpdated)
);

-- Transactions table
CREATE TABLE Transactions (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    TxHash NVARCHAR(100) NOT NULL UNIQUE,
    FromAddress NVARCHAR(100) NOT NULL,
    ToAddress NVARCHAR(100) NOT NULL,
    Amount DECIMAL(38,18) NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    TransactionType NVARCHAR(50) NOT NULL,
    ServiceId INT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'pending',
    GasUsed BIGINT NOT NULL DEFAULT 0,
    GasFee DECIMAL(38,18) NOT NULL DEFAULT 0,
    Note NVARCHAR(500) NULL,
    BlockNumber BIGINT NULL,
    BlockHash NVARCHAR(100) NULL,
    
    INDEX IX_Transactions_TxHash (TxHash),
    INDEX IX_Transactions_FromAddress (FromAddress),
    INDEX IX_Transactions_ToAddress (ToAddress),
    INDEX IX_Transactions_Timestamp (Timestamp),
    INDEX IX_Transactions_Status (Status)
);

-- Blocks table
CREATE TABLE Blocks (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    BlockNumber BIGINT NOT NULL UNIQUE,
    BlockHash NVARCHAR(100) NOT NULL UNIQUE,
    ParentHash NVARCHAR(100) NOT NULL,
    Timestamp DATETIME2 NOT NULL,
    TransactionCount INT NOT NULL DEFAULT 0,
    GasUsed BIGINT NOT NULL DEFAULT 0,
    GasLimit BIGINT NOT NULL DEFAULT 0,
    Miner NVARCHAR(100) NOT NULL,
    Difficulty BIGINT NOT NULL DEFAULT 0,
    TotalDifficulty BIGINT NOT NULL DEFAULT 0,
    Size BIGINT NOT NULL DEFAULT 0,
    
    INDEX IX_Blocks_BlockNumber (BlockNumber),
    INDEX IX_Blocks_BlockHash (BlockHash),
    INDEX IX_Blocks_Timestamp (Timestamp)
);

-- Services table
CREATE TABLE Services (
    Id INT IDENTITY(0,1) PRIMARY KEY,
    ServiceName NVARCHAR(100) NOT NULL,
    ServiceAddress NVARCHAR(100) NOT NULL,
    BaseFee DECIMAL(38,18) NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Services_ServiceName (ServiceName),
    INDEX IX_Services_ServiceAddress (ServiceAddress)
);

-- Insert default services
INSERT INTO Services (Id, ServiceName, ServiceAddress, BaseFee, IsActive) VALUES
(0, 'IP Patent', '0x1111111111111111111111111111111111111111', 100.00, 1),
(1, 'LKS Summit', '0x2222222222222222222222222222222222222222', 50.00, 1),
(2, 'Software Factory', '0x3333333333333333333333333333333333333333', 200.00, 1),
(3, 'Vara Security', '0x4444444444444444444444444444444444444444', 150.00, 1),
(4, 'Stadium Tackle', '0x5555555555555555555555555555555555555555', 25.00, 1),
(5, 'LKS Capital', '0x6666666666666666666666666666666666666666', 75.00, 1);

-- Network statistics table
CREATE TABLE NetworkStats (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    BlockNumber BIGINT NOT NULL,
    TotalTransactions BIGINT NOT NULL,
    ActiveWallets INT NOT NULL,
    TotalVolume DECIMAL(38,18) NOT NULL,
    TPS DECIMAL(10,2) NOT NULL,
    
    INDEX IX_NetworkStats_Timestamp (Timestamp)
);

-- API keys table for external integrations
CREATE TABLE ApiKeys (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    KeyName NVARCHAR(100) NOT NULL,
    ApiKey NVARCHAR(500) NOT NULL,
    Service NVARCHAR(100) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NULL,
    
    INDEX IX_ApiKeys_KeyName (KeyName),
    INDEX IX_ApiKeys_Service (Service)
);

-- Validators table for PoI consensus
CREATE TABLE Validators (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ValidatorAddress NVARCHAR(100) NOT NULL UNIQUE,
    StakedAmount DECIMAL(38,18) NOT NULL DEFAULT 0,
    AIScore DECIMAL(10,4) NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    LastActiveBlock BIGINT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_Validators_ValidatorAddress (ValidatorAddress),
    INDEX IX_Validators_StakedAmount (StakedAmount),
    INDEX IX_Validators_AIScore (AIScore)
);

-- Smart contracts table
CREATE TABLE SmartContracts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ContractAddress NVARCHAR(100) NOT NULL UNIQUE,
    ContractName NVARCHAR(200) NOT NULL,
    ABI NTEXT NOT NULL,
    Bytecode NTEXT NOT NULL,
    DeployedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DeployedBy NVARCHAR(100) NOT NULL,
    IsVerified BIT NOT NULL DEFAULT 0,
    
    INDEX IX_SmartContracts_ContractAddress (ContractAddress),
    INDEX IX_SmartContracts_ContractName (ContractName)
);

-- Insert core smart contracts
INSERT INTO SmartContracts (ContractAddress, ContractName, ABI, Bytecode, DeployedBy, IsVerified) VALUES
('0x742d35Cc6634C0532925a3b8D4C9db96', 'LKS COIN', '[{"constant":true,"inputs":[{"name":"_owner","type":"address"}],"name":"balanceOf","outputs":[{"name":"balance","type":"uint256"}],"type":"function"}]', '0x608060405234801561001057600080fd5b50', '0x0000000000000000000000000000000000000000', 1),
('0x1234567890123456789012345678901234567890', 'Universal Payment System', '[{"constant":false,"inputs":[{"name":"_service","type":"uint8"},{"name":"_amount","type":"uint256"},{"name":"_recipient","type":"address"}],"name":"processPayment","outputs":[{"name":"","type":"bool"}],"type":"function"}]', '0x608060405234801561001057600080fd5b50', '0x0000000000000000000000000000000000000000', 1);

-- Security audit log
CREATE TABLE SecurityAuditLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EventType NVARCHAR(100) NOT NULL,
    Severity NVARCHAR(20) NOT NULL,
    Message NVARCHAR(1000) NOT NULL,
    UserAddress NVARCHAR(100) NULL,
    IPAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    
    INDEX IX_SecurityAuditLog_Timestamp (Timestamp),
    INDEX IX_SecurityAuditLog_EventType (EventType),
    INDEX IX_SecurityAuditLog_Severity (Severity)
);

-- Create stored procedures for common operations

-- Get wallet balance with transaction history
CREATE PROCEDURE GetWalletDetails
    @Address NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Get wallet info
    SELECT Address, LKSBalance, ETHBalance, LastUpdated, IsActive, CreatedAt
    FROM Wallets 
    WHERE Address = @Address;
    
    -- Get recent transactions
    SELECT TOP 10 TxHash, FromAddress, ToAddress, Amount, Timestamp, 
           TransactionType, ServiceId, Status, GasUsed, GasFee, Note
    FROM Transactions 
    WHERE FromAddress = @Address OR ToAddress = @Address
    ORDER BY Timestamp DESC;
END;

-- Process transaction with balance update
CREATE PROCEDURE ProcessTransaction
    @TxHash NVARCHAR(100),
    @FromAddress NVARCHAR(100),
    @ToAddress NVARCHAR(100),
    @Amount DECIMAL(38,18),
    @TransactionType NVARCHAR(50),
    @ServiceId INT = NULL,
    @Note NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Insert transaction
        INSERT INTO Transactions (TxHash, FromAddress, ToAddress, Amount, TransactionType, ServiceId, Status, Note)
        VALUES (@TxHash, @FromAddress, @ToAddress, @Amount, @TransactionType, @ServiceId, 'confirmed', @Note);
        
        -- Update sender balance
        UPDATE Wallets 
        SET LKSBalance = LKSBalance - @Amount, LastUpdated = GETUTCDATE()
        WHERE Address = @FromAddress;
        
        -- Update receiver balance (if not a service payment)
        IF @TransactionType != 'payment'
        BEGIN
            UPDATE Wallets 
            SET LKSBalance = LKSBalance + @Amount, LastUpdated = GETUTCDATE()
            WHERE Address = @ToAddress;
        END
        
        COMMIT TRANSACTION;
        SELECT 1 as Success;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SELECT 0 as Success, ERROR_MESSAGE() as ErrorMessage;
    END CATCH
END;

-- Get network statistics
CREATE PROCEDURE GetNetworkStatistics
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        COUNT(DISTINCT Address) as TotalWallets,
        SUM(LKSBalance) as TotalLKSInWallets,
        (SELECT COUNT(*) FROM Transactions WHERE Timestamp >= DATEADD(day, -1, GETUTCDATE())) as TransactionsLast24h,
        (SELECT COUNT(*) FROM Transactions) as TotalTransactions,
        (SELECT MAX(BlockNumber) FROM Blocks) as LatestBlock,
        (SELECT COUNT(*) FROM Validators WHERE IsActive = 1) as ActiveValidators
    FROM Wallets 
    WHERE IsActive = 1;
END;
