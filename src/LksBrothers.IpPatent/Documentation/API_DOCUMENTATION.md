# LKS Brothers IP PATENT System - API Documentation

## Overview

The LKS Brothers IP PATENT System provides a comprehensive REST API for managing intellectual property patent submissions, reviews, and blockchain publishing. This API is designed for enterprise-grade security, scalability, and integration with the LKS Network ecosystem.

## Base URL

```
Production: https://api.lksnetwork.io/ip-patent/v1
Staging: https://staging-api.lksnetwork.io/ip-patent/v1
Development: https://localhost:5001/api
```

## Authentication

The API supports multiple authentication methods:

### 1. API Key Authentication
```http
Authorization: Bearer lks_your_api_key_here
```

### 2. JWT Token Authentication
```http
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 3. OAuth 2.0
```http
Authorization: Bearer oauth_access_token
```

## Rate Limiting

All API endpoints are subject to rate limiting based on your API key scope:

| Scope | Requests/Hour | Burst Limit |
|-------|---------------|-------------|
| ReadOnly | 1,000 | 50/min |
| Standard | 500 | 25/min |
| Premium | 2,000 | 100/min |
| Admin | 5,000 | 250/min |

Rate limit headers are included in all responses:
```http
X-Rate-Limit-Limit: 1000
X-Rate-Limit-Remaining: 999
X-Rate-Limit-Reset: 1640995200
```

## Error Handling

The API uses standard HTTP status codes and returns detailed error information:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid submission data",
    "details": {
      "field": "title",
      "reason": "Title is required and must be between 10-200 characters"
    },
    "timestamp": "2024-01-15T10:30:00Z",
    "requestId": "req_123456789"
  }
}
```

### Common Error Codes

| Code | Status | Description |
|------|--------|-------------|
| `INVALID_API_KEY` | 401 | API key is invalid or expired |
| `RATE_LIMIT_EXCEEDED` | 429 | Rate limit exceeded |
| `VALIDATION_ERROR` | 400 | Request validation failed |
| `RESOURCE_NOT_FOUND` | 404 | Requested resource not found |
| `INSUFFICIENT_PERMISSIONS` | 403 | Insufficient permissions |
| `INTERNAL_ERROR` | 500 | Internal server error |

## API Endpoints

### Submissions

#### Create New Submission
```http
POST /api/submissions
```

**Request Body:**
```json
{
  "title": "Revolutionary AI Algorithm for Patent Analysis",
  "description": "A comprehensive description of the invention...",
  "category": "Software",
  "inventors": [
    {
      "name": "John Doe",
      "email": "john.doe@example.com",
      "role": "Lead Inventor"
    }
  ],
  "documents": [
    {
      "type": "specification",
      "filename": "patent_spec.pdf",
      "content": "base64_encoded_content"
    }
  ],
  "claims": [
    "A method for analyzing patent documents using artificial intelligence...",
    "The method of claim 1, wherein the AI algorithm utilizes..."
  ],
  "priorArt": [
    {
      "title": "Existing Patent Title",
      "patentNumber": "US1234567",
      "relevance": "Similar approach but different implementation"
    }
  ],
  "metadata": {
    "priority": "high",
    "confidential": true,
    "estimatedValue": 1000000
  }
}
```

**Response:**
```json
{
  "submissionId": "sub_123456789",
  "status": "pending_review",
  "submissionDate": "2024-01-15T10:30:00Z",
  "reviewDeadline": "2024-01-22T10:30:00Z",
  "estimatedProcessingTime": "7-14 days",
  "trackingUrl": "https://portal.lksnetwork.io/submissions/sub_123456789"
}
```

#### Get Submission Details
```http
GET /api/submissions/{submissionId}
```

**Response:**
```json
{
  "submissionId": "sub_123456789",
  "title": "Revolutionary AI Algorithm for Patent Analysis",
  "status": "under_review",
  "submissionDate": "2024-01-15T10:30:00Z",
  "lastUpdated": "2024-01-16T14:20:00Z",
  "currentStage": "technical_review",
  "progress": {
    "percentage": 45,
    "stages": [
      {
        "name": "initial_review",
        "status": "completed",
        "completedAt": "2024-01-15T16:00:00Z"
      },
      {
        "name": "technical_review",
        "status": "in_progress",
        "startedAt": "2024-01-16T09:00:00Z",
        "estimatedCompletion": "2024-01-18T17:00:00Z"
      },
      {
        "name": "legal_review",
        "status": "pending"
      },
      {
        "name": "final_approval",
        "status": "pending"
      }
    ]
  },
  "reviewers": [
    {
      "name": "Dr. Sarah Johnson",
      "role": "Technical Reviewer",
      "specialization": "AI/ML Patents"
    }
  ],
  "documents": [
    {
      "id": "doc_123",
      "type": "specification",
      "filename": "patent_spec.pdf",
      "uploadDate": "2024-01-15T10:30:00Z",
      "status": "approved"
    }
  ],
  "comments": [
    {
      "id": "comment_456",
      "author": "Dr. Sarah Johnson",
      "timestamp": "2024-01-16T14:20:00Z",
      "type": "technical_feedback",
      "message": "The algorithm description needs more detail on the training methodology.",
      "status": "open"
    }
  ]
}
```

#### List Submissions
```http
GET /api/submissions?status=pending_review&page=1&limit=20
```

**Query Parameters:**
- `status`: Filter by submission status
- `category`: Filter by patent category
- `dateFrom`: Filter submissions from date (ISO 8601)
- `dateTo`: Filter submissions to date (ISO 8601)
- `page`: Page number (default: 1)
- `limit`: Items per page (default: 20, max: 100)
- `sort`: Sort field (submissionDate, title, status)
- `order`: Sort order (asc, desc)

#### Update Submission
```http
PUT /api/submissions/{submissionId}
```

#### Delete Submission
```http
DELETE /api/submissions/{submissionId}
```

### Reviews

#### Get Review Details
```http
GET /api/reviews/{reviewId}
```

#### Submit Review Decision
```http
POST /api/reviews/{reviewId}/decision
```

**Request Body:**
```json
{
  "decision": "approved",
  "comments": "The patent application meets all technical requirements.",
  "recommendations": [
    "Consider adding more detailed implementation examples",
    "Strengthen the claims section"
  ],
  "nextStage": "legal_review"
}
```

#### List Reviews
```http
GET /api/reviews?assignee=current&status=pending
```

### Blockchain Operations

#### Publish to Blockchain
```http
POST /api/blockchain/publish/{submissionId}
```

**Request Body:**
```json
{
  "networks": ["ethereum", "polygon", "bsc"],
  "metadata": {
    "ipfsHash": "QmX7M9CiYXjVzn1fzP2bQ8kL3mN4oP5qR6sT7uV8wX9yZ",
    "priority": "high"
  }
}
```

**Response:**
```json
{
  "publishId": "pub_123456789",
  "status": "publishing",
  "networks": [
    {
      "name": "ethereum",
      "status": "pending",
      "transactionHash": null,
      "estimatedCompletion": "2024-01-15T11:00:00Z"
    },
    {
      "name": "polygon",
      "status": "confirmed",
      "transactionHash": "0x1234567890abcdef...",
      "blockNumber": 12345678,
      "gasUsed": 150000
    }
  ],
  "totalNetworks": 3,
  "completedNetworks": 1,
  "estimatedTotalTime": "15-30 minutes"
}
```

#### Get Blockchain Status
```http
GET /api/blockchain/status/{publishId}
```

### Analytics

#### Get Submission Analytics
```http
GET /api/analytics/submissions?period=30d
```

**Response:**
```json
{
  "period": "30d",
  "totalSubmissions": 156,
  "approvedSubmissions": 89,
  "rejectedSubmissions": 23,
  "pendingSubmissions": 44,
  "averageProcessingTime": "12.5 days",
  "categoryBreakdown": {
    "Software": 67,
    "Hardware": 34,
    "Biotechnology": 28,
    "Chemical": 27
  },
  "dailyStats": [
    {
      "date": "2024-01-01",
      "submissions": 8,
      "approvals": 5,
      "rejections": 1
    }
  ]
}
```

### API Key Management

#### Generate API Key
```http
POST /api/apikey/generate
```

**Request Body:**
```json
{
  "description": "Production API key for mobile app",
  "scope": "Standard"
}
```

**Response:**
```json
{
  "apiKey": "lks_1234567890abcdef1234567890abcdef",
  "keyId": "key_123456789",
  "description": "Production API key for mobile app",
  "scope": "Standard",
  "expiresAt": "2025-01-15T10:30:00Z",
  "rateLimitPerHour": 500
}
```

#### List API Keys
```http
GET /api/apikey/my-keys
```

#### Revoke API Key
```http
DELETE /api/apikey/{keyId}
```

### Backup Operations

#### Create Backup
```http
POST /api/backup/full
```

#### Get Backup Status
```http
GET /api/backup/status/{backupId}
```

#### List Backups
```http
GET /api/backup/history
```

## Webhooks

The API supports webhooks for real-time notifications:

### Webhook Events

| Event | Description |
|-------|-------------|
| `submission.created` | New submission received |
| `submission.updated` | Submission status changed |
| `review.assigned` | Review assigned to reviewer |
| `review.completed` | Review decision submitted |
| `blockchain.published` | Patent published to blockchain |
| `blockchain.failed` | Blockchain publishing failed |

### Webhook Payload Example

```json
{
  "event": "submission.updated",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    "submissionId": "sub_123456789",
    "status": "approved",
    "previousStatus": "under_review",
    "updatedBy": "reviewer_456"
  },
  "signature": "sha256=1234567890abcdef..."
}
```

### Webhook Configuration

```http
POST /api/webhooks
```

**Request Body:**
```json
{
  "url": "https://your-app.com/webhooks/ip-patent",
  "events": ["submission.updated", "blockchain.published"],
  "secret": "your_webhook_secret"
}
```

## SDK Examples

### JavaScript/Node.js

```javascript
const IpPatentAPI = require('@lks-brothers/ip-patent-sdk');

const client = new IpPatentAPI({
  apiKey: 'lks_your_api_key_here',
  environment: 'production'
});

// Create a new submission
const submission = await client.submissions.create({
  title: 'My Patent Application',
  description: 'Detailed description...',
  category: 'Software',
  inventors: [{ name: 'John Doe', email: 'john@example.com' }]
});

console.log('Submission created:', submission.submissionId);

// Track submission progress
const status = await client.submissions.get(submission.submissionId);
console.log('Current status:', status.status);
```

### Python

```python
from lks_brothers import IpPatentClient

client = IpPatentClient(
    api_key='lks_your_api_key_here',
    environment='production'
)

# Create submission
submission = client.submissions.create({
    'title': 'My Patent Application',
    'description': 'Detailed description...',
    'category': 'Software',
    'inventors': [{'name': 'John Doe', 'email': 'john@example.com'}]
})

print(f"Submission created: {submission['submissionId']}")
```

### cURL

```bash
# Create a new submission
curl -X POST https://api.lksnetwork.io/ip-patent/v1/api/submissions \
  -H "Authorization: Bearer lks_your_api_key_here" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "My Patent Application",
    "description": "Detailed description...",
    "category": "Software",
    "inventors": [{"name": "John Doe", "email": "john@example.com"}]
  }'

# Get submission status
curl -X GET https://api.lksnetwork.io/ip-patent/v1/api/submissions/sub_123456789 \
  -H "Authorization: Bearer lks_your_api_key_here"
```

## Testing

### Test Environment

Use the staging environment for testing:
```
Base URL: https://staging-api.lksnetwork.io/ip-patent/v1
```

### Test API Keys

Request test API keys from the developer portal:
- ReadOnly: `lks_test_readonly_key`
- Standard: `lks_test_standard_key`

### Postman Collection

Download our Postman collection: [IP PATENT API Collection](https://api.lksnetwork.io/docs/postman/ip-patent.json)

## Support

- **Documentation**: https://docs.lksnetwork.io/ip-patent
- **Developer Portal**: https://developers.lksnetwork.io
- **Support Email**: api-support@lksnetwork.io
- **Status Page**: https://status.lksnetwork.io

## Changelog

### v1.2.0 (2024-01-15)
- Added real-time status tracking
- Enhanced security middleware
- Improved rate limiting
- Added webhook support

### v1.1.0 (2023-12-01)
- Added blockchain publishing
- Enhanced analytics
- API key management
- Backup operations

### v1.0.0 (2023-10-01)
- Initial API release
- Basic submission management
- Review workflow
- Authentication system
