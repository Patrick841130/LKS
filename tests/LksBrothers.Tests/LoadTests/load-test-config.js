import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
export let errorRate = new Rate('errors');

// Load test configuration
export let options = {
  stages: [
    { duration: '2m', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 200 }, // Ramp up to 200 users
    { duration: '5m', target: 200 }, // Stay at 200 users
    { duration: '2m', target: 500 }, // Ramp up to 500 users
    { duration: '5m', target: 500 }, // Stay at 500 users
    { duration: '2m', target: 1000 }, // Ramp up to 1000 users
    { duration: '10m', target: 1000 }, // Stay at 1000 users for stress test
    { duration: '5m', target: 0 }, // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% of requests must complete below 500ms
    http_req_failed: ['rate<0.1'], // Error rate must be below 10%
    errors: ['rate<0.1'], // Custom error rate must be below 10%
  },
};

const BASE_URL = 'https://lksnetwork.io/lks-network';
const API_BASE = `${BASE_URL}/api`;

export default function () {
  // Test scenarios with realistic user behavior
  let scenario = Math.random();
  
  if (scenario < 0.4) {
    // 40% - Browse explorer homepage
    testExplorerHomepage();
  } else if (scenario < 0.6) {
    // 20% - View latest blocks
    testLatestBlocks();
  } else if (scenario < 0.8) {
    // 20% - Search functionality
    testSearchFunctionality();
  } else if (scenario < 0.9) {
    // 10% - View tokenomics
    testTokenomicsPage();
  } else {
    // 10% - API stress test
    testAPIEndpoints();
  }
  
  // Realistic user think time
  sleep(Math.random() * 3 + 1);
}

function testExplorerHomepage() {
  let response = http.get(BASE_URL);
  
  let success = check(response, {
    'homepage loads successfully': (r) => r.status === 200,
    'homepage loads within 2s': (r) => r.timings.duration < 2000,
    'homepage contains LKS branding': (r) => r.body.includes('LKS'),
  });
  
  errorRate.add(!success);
  
  // Load network stats
  let statsResponse = http.get(`${API_BASE}/explorer/stats`);
  check(statsResponse, {
    'stats API responds': (r) => r.status === 200,
    'stats API fast response': (r) => r.timings.duration < 500,
  });
}

function testLatestBlocks() {
  let response = http.get(`${API_BASE}/explorer/blocks/latest?count=20`);
  
  let success = check(response, {
    'blocks API responds': (r) => r.status === 200,
    'blocks API fast response': (r) => r.timings.duration < 300,
    'blocks data is valid JSON': (r) => {
      try {
        JSON.parse(r.body);
        return true;
      } catch (e) {
        return false;
      }
    },
  });
  
  errorRate.add(!success);
}

function testSearchFunctionality() {
  // Test search with various queries
  const searchQueries = [
    '0x1234567890abcdef',
    'block',
    '12345',
    'latest'
  ];
  
  let query = searchQueries[Math.floor(Math.random() * searchQueries.length)];
  let response = http.get(`${API_BASE}/explorer/search?query=${query}`);
  
  let success = check(response, {
    'search API responds': (r) => r.status === 200 || r.status === 404,
    'search API fast response': (r) => r.timings.duration < 400,
  });
  
  errorRate.add(!success);
}

function testTokenomicsPage() {
  let response = http.get(`${BASE_URL}/tokenomics.html`);
  
  let success = check(response, {
    'tokenomics page loads': (r) => r.status === 200,
    'tokenomics loads within 3s': (r) => r.timings.duration < 3000,
    'tokenomics contains charts': (r) => r.body.includes('d3.js'),
    'tokenomics contains LKS COIN': (r) => r.body.includes('LKS COIN'),
  });
  
  errorRate.add(!success);
}

function testAPIEndpoints() {
  // Test multiple API endpoints in sequence
  const endpoints = [
    '/explorer/stats',
    '/explorer/blocks/latest?count=10',
    '/explorer/transactions?page=1&pageSize=10',
  ];
  
  endpoints.forEach(endpoint => {
    let response = http.get(`${API_BASE}${endpoint}`);
    
    let success = check(response, {
      [`${endpoint} responds`]: (r) => r.status === 200,
      [`${endpoint} fast response`]: (r) => r.timings.duration < 500,
    });
    
    errorRate.add(!success);
  });
}

// Spike test configuration for extreme load
export let spikeOptions = {
  stages: [
    { duration: '1m', target: 100 },
    { duration: '30s', target: 2000 }, // Sudden spike
    { duration: '1m', target: 2000 },
    { duration: '30s', target: 100 }, // Drop back down
    { duration: '1m', target: 100 },
  ],
};

// Stress test configuration
export let stressOptions = {
  stages: [
    { duration: '5m', target: 500 },
    { duration: '10m', target: 1000 },
    { duration: '10m', target: 1500 },
    { duration: '10m', target: 2000 },
    { duration: '5m', target: 0 },
  ],
};
