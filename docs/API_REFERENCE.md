# 📚 LKS Network API Reference

## **Overview**

Complete API documentation for LKS Network mainnet blockchain explorer and infrastructure management.

**Base URLs:**
- **Explorer API**: `https://lksnetwork.io/lks-network/api`
- **Admin API**: `https://admin.lksnetwork.io/api`
- **RPC Endpoint**: `https://rpc.lksnetwork.io`

---

## **🔍 Explorer API**

### **Network Statistics**

#### `GET /explorer/stats`
Get current network statistics and metrics.

**Response:**
```json
{
  "blockHeight": 1234567,
  "totalTransactions": 9876543,
  "activeValidators": 150,
  "networkHashRate": "1.2 TH/s",
  "averageBlockTime": 400,
  "transactionsPerSecond": 65000,
  "totalSupply": "1000000000",
  "circulatingSupply": "650000000",
  "stakingRatio": 0.45,
  "networkHealth": "healthy"
}
```

### **Blocks**

#### `GET /explorer/blocks/latest`
Get latest blocks from the blockchain.

**Parameters:**
- `count` (optional): Number of blocks to return (default: 10, max: 100)

**Response:**
```json
[
  {
    "number": 1234567,
    "hash": "0x1234567890abcdef...",
    "parentHash": "0xabcdef1234567890...",
    "timestamp": "2024-09-01T14:30:00Z",
    "transactionCount": 150,
    "proposer": "0xvalidator123...",
    "size": 2048,
    "gasUsed": 21000000,
    "gasLimit": 30000000
  }
]
```

#### `GET /explorer/blocks/{blockNumber}`
Get specific block by number.

**Parameters:**
- `blockNumber`: Block number or "latest"

**Response:**
```json
{
  "number": 1234567,
  "hash": "0x1234567890abcdef...",
  "parentHash": "0xabcdef1234567890...",
  "timestamp": "2024-09-01T14:30:00Z",
  "transactions": [
    {
      "hash": "0xtx123...",
      "from": "0xsender...",
      "to": "0xrecipient...",
      "value": "1000000000000000000",
      "gasUsed": 21000,
      "status": "success"
    }
  ],
  "proposer": "0xvalidator123...",
  "signature": "0xsig123..."
}
```

### **Transactions**

#### `GET /explorer/transactions`
Get paginated list of transactions.

**Parameters:**
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20, max: 100)
- `address` (optional): Filter by address

**Response:**
```json
{
  "transactions": [
    {
      "hash": "0xtx123...",
      "blockNumber": 1234567,
      "blockHash": "0xblock123...",
      "transactionIndex": 0,
      "from": "0xsender...",
      "to": "0xrecipient...",
      "value": "1000000000000000000",
      "gasPrice": "0",
      "gasUsed": 21000,
      "timestamp": "2024-09-01T14:30:00Z",
      "status": "success",
      "type": "transfer"
    }
  ],
  "totalCount": 9876543,
  "page": 1,
  "pageSize": 20,
  "totalPages": 493827
}
```

#### `GET /explorer/transactions/{hash}`
Get specific transaction by hash.

**Response:**
```json
{
  "hash": "0xtx123...",
  "blockNumber": 1234567,
  "blockHash": "0xblock123...",
  "from": "0xsender...",
  "to": "0xrecipient...",
  "value": "1000000000000000000",
  "gasPrice": "0",
  "gasUsed": 21000,
  "timestamp": "2024-09-01T14:30:00Z",
  "status": "success",
  "logs": [],
  "receipt": {
    "status": 1,
    "gasUsed": 21000,
    "logs": []
  }
}
```

### **Search**

#### `GET /explorer/search`
Search for blocks, transactions, or addresses.

**Parameters:**
- `query`: Search term (block number, transaction hash, or address)

**Response:**
```json
{
  "type": "transaction",
  "result": {
    "hash": "0xtx123...",
    "blockNumber": 1234567,
    "from": "0xsender...",
    "to": "0xrecipient...",
    "value": "1000000000000000000"
  }
}
```

### **Validators**

#### `GET /explorer/validators`
Get list of active validators.

**Response:**
```json
[
  {
    "address": "0xvalidator123...",
    "publicKey": "0xpubkey123...",
    "stake": "32000000000000000000",
    "commission": 0.05,
    "uptime": 0.999,
    "blocksProposed": 1250,
    "status": "active",
    "joinedAt": "2024-01-01T00:00:00Z"
  }
]
```

---

## **🔧 Admin API**

### **Authentication**
All admin endpoints require JWT authentication.

**Header:**
```
Authorization: Bearer <jwt_token>
```

### **Dashboard**

#### `GET /admin/dashboard`
Get comprehensive admin dashboard data.

**Response:**
```json
{
  "metrics": {
    "activeUsers": 1250,
    "runningNodes": 5,
    "totalNodes": 5,
    "averageNodeCpuUsage": 45.2,
    "averageNodeMemoryUsage": 62.8,
    "networkThroughput": 125000000,
    "transactionsPerSecond": 45000
  },
  "alerts": [
    {
      "type": "HighResourceUsage",
      "severity": "Warning",
      "message": "Node node-1 CPU usage is 85.5%",
      "timestamp": "2024-09-01T14:30:00Z",
      "nodeId": "node-1"
    }
  ],
  "systemStatus": "Healthy",
  "lastUpdated": "2024-09-01T14:30:00Z"
}
```

### **Node Management**

#### `POST /admin/nodes`
Create a new blockchain node.

**Request Body:**
```json
{
  "dataDirectory": "/app/data/node-6",
  "isValidator": true,
  "validatorKeyPath": "/app/keys/validator-6.json",
  "networkId": "mainnet",
  "port": 8086,
  "rpcPort": 8551,
  "bootstrapNodes": ["node1.lksnetwork.io:8080"]
}
```

**Response:**
```json
{
  "id": "node-6",
  "configuration": {
    "dataDirectory": "/app/data/node-6",
    "isValidator": true,
    "port": 8086
  },
  "status": "Starting",
  "createdAt": "2024-09-01T14:30:00Z"
}
```

#### `POST /admin/nodes/scale`
Scale the number of running nodes.

**Request Body:**
```json
{
  "targetCount": 10,
  "reason": "Increased user load"
}
```

**Response:**
```json
{
  "message": "Successfully scaled to 10 nodes",
  "previousCount": 5,
  "newCount": 10
}
```

#### `PUT /admin/nodes/{nodeId}/config`
Update node configuration.

**Request Body:**
```json
{
  "port": 8087,
  "rpcPort": 8552,
  "customSettings": {
    "maxPeers": 50,
    "logLevel": "info"
  }
}
```

### **System Health**

#### `GET /admin/system-health`
Get comprehensive system health report.

**Response:**
```json
{
  "overallStatus": "Healthy",
  "nodeHealth": "Healthy",
  "userLoadHealth": "Warning",
  "resourceHealth": "Healthy",
  "criticalAlerts": [],
  "recommendations": [
    "Consider scaling up nodes to handle increased user load."
  ],
  "timestamp": "2024-09-01T14:30:00Z"
}
```

### **Emergency Controls**

#### `POST /admin/maintenance`
Enter maintenance mode.

**Request Body:**
```json
{
  "reason": "Scheduled maintenance",
  "scheduledStart": "2024-09-01T20:00:00Z",
  "estimatedDuration": "PT2H"
}
```

#### `POST /admin/emergency-stop`
Emergency stop all services.

**Request Body:**
```json
{
  "reason": "Security incident detected",
  "authorizedBy": "admin@lksnetwork.io"
}
```

---

## **⚡ JSON-RPC API**

### **Standard Methods**

#### `lks_getBalance`
Get account balance.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "lks_getBalance",
  "params": ["0xaddress123...", "latest"],
  "id": 1
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "result": "0x1bc16d674ec80000",
  "id": 1
}
```

#### `lks_sendTransaction`
Send a transaction.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "lks_sendTransaction",
  "params": [{
    "from": "0xsender...",
    "to": "0xrecipient...",
    "value": "0x1bc16d674ec80000",
    "gas": "0x5208",
    "gasPrice": "0x0"
  }],
  "id": 1
}
```

#### `lks_getBlockByNumber`
Get block by number.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "lks_getBlockByNumber",
  "params": ["latest", true],
  "id": 1
}
```

#### `lks_getTransactionReceipt`
Get transaction receipt.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "lks_getTransactionReceipt",
  "params": ["0xtxhash..."],
  "id": 1
}
```

---

## **🔒 Authentication**

### **Admin Authentication**
Admin endpoints use JWT tokens:

1. Obtain token from authentication service
2. Include in Authorization header: `Bearer <token>`
3. Tokens expire after 24 hours

### **Rate Limiting**
- **Explorer API**: 30 requests/second per IP
- **Admin API**: 10 requests/second per authenticated user
- **RPC API**: 100 requests/second per IP

---

## **📊 Response Codes**

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Not Found |
| 429 | Too Many Requests |
| 500 | Internal Server Error |
| 503 | Service Unavailable |

---

## **🔧 Error Handling**

**Standard Error Response:**
```json
{
  "error": {
    "code": 400,
    "message": "Invalid block number",
    "details": "Block number must be a positive integer or 'latest'"
  },
  "timestamp": "2024-09-01T14:30:00Z",
  "path": "/api/explorer/blocks/invalid"
}
```

---

## **📈 Performance**

### **Response Times**
- **Explorer API**: < 200ms (95th percentile)
- **Admin API**: < 500ms (95th percentile)
- **RPC API**: < 100ms (95th percentile)

### **Throughput**
- **65,000+ TPS**: Transaction processing capacity
- **1000+ concurrent users**: Supported simultaneously
- **99.9% uptime**: Service availability target

---

## **🛠️ SDK Examples**

### **JavaScript/TypeScript**
```javascript
// Explorer API client
const explorer = new LKSExplorer('https://lksnetwork.io/lks-network/api');

// Get latest blocks
const blocks = await explorer.getLatestBlocks(10);

// Search for transaction
const result = await explorer.search('0xtxhash...');
```

### **Python**
```python
import requests

# Get network stats
response = requests.get('https://lksnetwork.io/lks-network/api/explorer/stats')
stats = response.json()

print(f"Block Height: {stats['blockHeight']}")
print(f"TPS: {stats['transactionsPerSecond']}")
```

### **cURL**
```bash
# Get latest blocks
curl -X GET "https://lksnetwork.io/lks-network/api/explorer/blocks/latest?count=5"

# Search for transaction
curl -X GET "https://lksnetwork.io/lks-network/api/explorer/search?query=0x123..."

# Admin: Scale nodes (requires auth)
curl -X POST "https://admin.lksnetwork.io/api/admin/nodes/scale" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"targetCount": 10}'
```

---

## **📞 Support**

- **Documentation**: https://docs.lksnetwork.io
- **API Status**: https://status.lksnetwork.io
- **Support Email**: support@lksnetwork.io
- **GitHub Issues**: https://github.com/lks-brothers/lks-network

**API Version**: v1.0.0  
**Last Updated**: September 1, 2024
