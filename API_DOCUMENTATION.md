# 📚 LKS NETWORK API Documentation

## 🦁 **Complete API Reference Guide**

**Version**: 1.0  
**Base URL**: `https://api.lksnetwork.com`  
**Authentication**: JWT Bearer Token  

---

## 🔐 **Authentication APIs**

### **POST** `/api/user/register`
Register a new user account.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "walletAddress": "rN7n7otQDd6FczFgLdSqtcsAUxDkw6fzRH"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "message": "User registered successfully",
  "userId": "12345",
  "email": "user@example.com"
}
```

**Error Responses:**
- `400 Bad Request`: Invalid input data
- `409 Conflict`: Email already exists

---

### **POST** `/api/user/login`
Authenticate user and receive JWT token.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2025-08-30T13:03:43Z",
  "user": {
    "id": "12345",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "role": "User"
  }
}
```

**Error Responses:**
- `401 Unauthorized`: Invalid credentials
- `429 Too Many Requests`: Rate limit exceeded

---

### **POST** `/api/user/logout`
Logout user and invalidate token.

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

---

## 👤 **User Management APIs**

### **GET** `/api/user/profile`
Get current user profile information.

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Response (200 OK):**
```json
{
  "id": "12345",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "walletAddress": "rN7n7otQDd6FczFgLdSqtcsAUxDkw6fzRH",
  "role": "User",
  "isActive": true,
  "createdAt": "2025-08-29T10:00:00Z",
  "lastLoginAt": "2025-08-29T13:00:00Z",
  "preferences": {
    "theme": "dark",
    "notifications": true,
    "language": "en"
  }
}
```

---

### **PUT** `/api/user/profile`
Update user profile information.

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Request Body:**
```json
{
  "firstName": "John",
  "lastName": "Smith",
  "walletAddress": "rNewWalletAddress123456789",
  "preferences": {
    "theme": "light",
    "notifications": false,
    "language": "en"
  }
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "Profile updated successfully"
}
```

---

### **GET** `/api/user/activity`
Get user activity history.

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Query Parameters:**
- `page`: Page number (default: 1)
- `limit`: Items per page (default: 20, max: 100)
- `action`: Filter by action type

**Response (200 OK):**
```json
{
  "activities": [
    {
      "id": "67890",
      "action": "Login",
      "description": "User logged in successfully",
      "ipAddress": "192.168.1.100",
      "userAgent": "Mozilla/5.0...",
      "createdAt": "2025-08-29T13:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "totalPages": 5,
    "totalItems": 100,
    "hasNext": true,
    "hasPrevious": false
  }
}
```

---

## 🛡️ **Admin APIs** (Admin Role Required)

### **GET** `/api/admin/users`
Get list of all users (Admin only).

**Headers:**
```
Authorization: Bearer <admin_jwt_token>
```

**Query Parameters:**
- `page`: Page number (default: 1)
- `limit`: Items per page (default: 20)
- `search`: Search by email or name
- `role`: Filter by user role
- `status`: Filter by active/inactive

**Response (200 OK):**
```json
{
  "users": [
    {
      "id": "12345",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "User",
      "isActive": true,
      "createdAt": "2025-08-29T10:00:00Z",
      "lastLoginAt": "2025-08-29T13:00:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "totalPages": 10,
    "totalItems": 200
  }
}
```

---

### **PUT** `/api/admin/users/{userId}/role`
Update user role (Admin only).

**Headers:**
```
Authorization: Bearer <admin_jwt_token>
```

**Request Body:**
```json
{
  "role": "Admin"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "User role updated successfully"
}
```

---

### **DELETE** `/api/admin/users/{userId}`
Delete user account (Admin only).

**Headers:**
```
Authorization: Bearer <admin_jwt_token>
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "User deleted successfully"
}
```

**Error Responses:**
- `403 Forbidden`: Cannot delete admin users
- `404 Not Found`: User not found

---

## 💳 **Payment APIs**

### **POST** `/api/payment/send-xrp`
Send XRP payment through Ripple network.

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Request Body:**
```json
{
  "sourceAddress": "rSourceWalletAddress123456789",
  "destinationAddress": "rDestinationWalletAddress123456789",
  "amount": "10.5",
  "destinationTag": 12345,
  "memo": "Payment for services"
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "transactionHash": "A1B2C3D4E5F6G7H8I9J0K1L2M3N4O5P6Q7R8S9T0",
  "ledgerIndex": 75432100,
  "fee": "0.00001",
  "status": "validated"
}
```

**Error Responses:**
- `400 Bad Request`: Invalid payment parameters
- `402 Payment Required`: Insufficient funds
- `503 Service Unavailable`: Ripple network unavailable

---

### **GET** `/api/payment/balance/{address}`
Get XRP balance for wallet address.

**Response (200 OK):**
```json
{
  "address": "rN7n7otQDd6FczFgLdSqtcsAUxDkw6fzRH",
  "balances": [
    {
      "currency": "XRP",
      "value": "1063.062671"
    }
  ],
  "reserve": "10.0",
  "available": "1053.062671"
}
```

---

## 🔒 **Security APIs** (Admin Role Required)

### **POST** `/api/security/scan`
Trigger security scan of the system.

**Headers:**
```
Authorization: Bearer <admin_jwt_token>
```

**Response (200 OK):**
```json
{
  "success": true,
  "scanId": "scan_12345",
  "message": "Security scan initiated",
  "estimatedCompletion": "2025-08-29T13:10:00Z"
}
```

---

### **GET** `/api/security/status`
Get current security system status.

**Headers:**
```
Authorization: Bearer <admin_jwt_token>
```

**Response (200 OK):**
```json
{
  "systemStatus": "Secure",
  "lastScan": "2025-08-29T12:30:00Z",
  "activeThreats": 0,
  "blockedIPs": 5,
  "securityLevel": "High",
  "encryptionStatus": "Active",
  "monitoringStatus": "Active",
  "uptime": "99.99%"
}
```

---

### **POST** `/api/security/block-ip`
Manually block an IP address.

**Headers:**
```
Authorization: Bearer <admin_jwt_token>
```

**Request Body:**
```json
{
  "ipAddress": "192.168.1.100",
  "reason": "Suspicious activity detected",
  "duration": 3600
}
```

**Response (200 OK):**
```json
{
  "success": true,
  "message": "IP 192.168.1.100 blocked successfully",
  "expiresAt": "2025-08-29T14:03:43Z"
}
```

---

### **GET** `/api/security/threats`
Get list of active security threats.

**Headers:**
```
Authorization: Bearer <admin_jwt_token>
```

**Response (200 OK):**
```json
{
  "threats": [
    {
      "id": "threat_001",
      "type": "Brute Force",
      "source": "192.168.1.100",
      "severity": "High",
      "firstDetected": "2025-08-29T12:45:00Z",
      "status": "Blocked",
      "attempts": 25
    }
  ],
  "totalCount": 1,
  "summary": {
    "high": 1,
    "medium": 0,
    "low": 0
  }
}
```

---

## 📊 **Explorer APIs**

### **GET** `/api/explorer/stats`
Get blockchain network statistics.

**Response (200 OK):**
```json
{
  "networkStats": {
    "totalBlocks": 1234567,
    "totalTransactions": 9876543210,
    "activeValidators": 150,
    "networkHashRate": "1.2 TH/s",
    "averageBlockTime": "400ms",
    "tps": 65000
  },
  "marketData": {
    "price": "$0.0025",
    "marketCap": "$125,000,000",
    "volume24h": "$2,500,000",
    "change24h": "+5.2%"
  }
}
```

---

### **GET** `/api/explorer/blocks`
Get list of recent blocks.

**Query Parameters:**
- `page`: Page number (default: 1)
- `limit`: Items per page (default: 20)

**Response (200 OK):**
```json
{
  "blocks": [
    {
      "height": 1234567,
      "hash": "0x1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p7q8r9s0t",
      "timestamp": "2025-08-29T13:03:43Z",
      "transactions": 150,
      "validator": "Validator001",
      "size": "2.5 MB",
      "gasUsed": "8500000"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "totalPages": 61728,
    "totalItems": 1234567
  }
}
```

---

### **GET** `/api/explorer/transactions`
Get list of recent transactions.

**Query Parameters:**
- `page`: Page number (default: 1)
- `limit`: Items per page (default: 20)
- `address`: Filter by address

**Response (200 OK):**
```json
{
  "transactions": [
    {
      "hash": "0xa1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0",
      "blockHeight": 1234567,
      "timestamp": "2025-08-29T13:03:43Z",
      "from": "0x1234567890abcdef1234567890abcdef12345678",
      "to": "0xabcdef1234567890abcdef1234567890abcdef12",
      "value": "100.0 LKS",
      "fee": "0.0 LKS",
      "status": "Success"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "totalPages": 493827150,
    "totalItems": 9876543000
  }
}
```

---

## 🔍 **Search APIs**

### **GET** `/api/search`
Search blocks, transactions, or addresses.

**Query Parameters:**
- `q`: Search query (required)
- `type`: Search type (block, transaction, address)

**Response (200 OK):**
```json
{
  "results": [
    {
      "type": "transaction",
      "hash": "0xa1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0",
      "blockHeight": 1234567,
      "timestamp": "2025-08-29T13:03:43Z"
    }
  ],
  "totalResults": 1,
  "searchTime": "0.05s"
}
```

---

## 🏥 **Health Check APIs**

### **GET** `/health`
System health check endpoint.

**Response (200 OK):**
```json
{
  "status": "healthy",
  "timestamp": "2025-08-29T13:03:43Z",
  "version": "1.0.0",
  "services": {
    "database": "healthy",
    "redis": "healthy",
    "blockchain": "healthy",
    "payment": "healthy"
  },
  "uptime": "72h 15m 30s"
}
```

---

## 📝 **Rate Limiting**

All API endpoints are subject to rate limiting:

| Endpoint Type | Rate Limit | Window |
|---------------|------------|--------|
| Authentication | 10 requests | 1 minute |
| User APIs | 100 requests | 1 minute |
| Payment APIs | 5 requests | 1 minute |
| Admin APIs | 50 requests | 1 minute |
| Explorer APIs | 200 requests | 1 minute |

**Rate Limit Headers:**
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1693315423
```

---

## 🚨 **Error Codes**

| Code | Description |
|------|-------------|
| 400 | Bad Request - Invalid input |
| 401 | Unauthorized - Invalid or missing token |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource not found |
| 409 | Conflict - Resource already exists |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error - Server error |
| 503 | Service Unavailable - Service temporarily down |

**Error Response Format:**
```json
{
  "success": false,
  "error": {
    "code": "INVALID_CREDENTIALS",
    "message": "Invalid email or password",
    "details": "Authentication failed"
  },
  "timestamp": "2025-08-29T13:03:43Z"
}
```

---

## 🔐 **Security Headers**

All API responses include security headers:

```
X-Frame-Options: DENY
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
Strict-Transport-Security: max-age=31536000
Content-Security-Policy: default-src 'self'
```

---

## 📖 **SDK Examples**

### **JavaScript/TypeScript**
```javascript
const LKSNetwork = require('@lksnetwork/sdk');

const client = new LKSNetwork({
  baseURL: 'https://api.lksnetwork.com',
  apiKey: 'your-api-key'
});

// Get user profile
const profile = await client.user.getProfile();

// Send XRP payment
const payment = await client.payment.sendXRP({
  sourceAddress: 'rSource...',
  destinationAddress: 'rDest...',
  amount: '10.5'
});
```

### **cURL Examples**
```bash
# Login
curl -X POST https://api.lksnetwork.com/api/user/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"password"}'

# Get profile
curl -X GET https://api.lksnetwork.com/api/user/profile \
  -H "Authorization: Bearer <token>"

# Send XRP payment
curl -X POST https://api.lksnetwork.com/api/payment/send-xrp \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"sourceAddress":"rSource...","destinationAddress":"rDest...","amount":"10.5"}'
```

---

## 🦁 **LKS NETWORK API - Complete Reference**

**Made in USA 🇺🇸 | Zero Transaction Fees | Enterprise Security**

*API Documentation v1.0 - Last Updated: August 29, 2025*
