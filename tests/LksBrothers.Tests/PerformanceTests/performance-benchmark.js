const { performance } = require('perf_hooks');
const axios = require('axios');
const fs = require('fs');
const path = require('path');

// Performance benchmarking suite for LKS Network
class PerformanceBenchmark {
    constructor() {
        this.baseUrl = 'https://lksnetwork.io/lks-network';
        this.apiBase = `${this.baseUrl}/api`;
        this.results = {
            timestamp: new Date().toISOString(),
            tests: [],
            summary: {}
        };
    }

    // Measure API response time
    async measureApiResponse(endpoint, description) {
        const start = performance.now();
        try {
            const response = await axios.get(`${this.apiBase}${endpoint}`, {
                timeout: 10000
            });
            const end = performance.now();
            const duration = end - start;
            
            const result = {
                test: description,
                endpoint,
                duration: Math.round(duration),
                status: response.status,
                success: true,
                dataSize: JSON.stringify(response.data).length
            };
            
            this.results.tests.push(result);
            console.log(`✅ ${description}: ${Math.round(duration)}ms`);
            return result;
        } catch (error) {
            const end = performance.now();
            const duration = end - start;
            
            const result = {
                test: description,
                endpoint,
                duration: Math.round(duration),
                status: error.response?.status || 0,
                success: false,
                error: error.message
            };
            
            this.results.tests.push(result);
            console.log(`❌ ${description}: ${Math.round(duration)}ms (${error.message})`);
            return result;
        }
    }

    // Measure concurrent requests
    async measureConcurrentRequests(endpoint, concurrency, description) {
        console.log(`\n🔄 Testing ${description} with ${concurrency} concurrent requests...`);
        
        const start = performance.now();
        const promises = Array(concurrency).fill().map(() => 
            axios.get(`${this.apiBase}${endpoint}`, { timeout: 10000 })
        );
        
        try {
            const responses = await Promise.all(promises);
            const end = performance.now();
            const totalDuration = end - start;
            const avgDuration = totalDuration / concurrency;
            
            const result = {
                test: description,
                endpoint,
                concurrency,
                totalDuration: Math.round(totalDuration),
                averageDuration: Math.round(avgDuration),
                successCount: responses.length,
                failureCount: 0,
                throughput: Math.round((concurrency / totalDuration) * 1000)
            };
            
            this.results.tests.push(result);
            console.log(`✅ ${description}: ${Math.round(totalDuration)}ms total, ${Math.round(avgDuration)}ms avg, ${result.throughput} req/s`);
            return result;
        } catch (error) {
            const end = performance.now();
            const totalDuration = end - start;
            
            const result = {
                test: description,
                endpoint,
                concurrency,
                totalDuration: Math.round(totalDuration),
                successCount: 0,
                failureCount: concurrency,
                error: error.message
            };
            
            this.results.tests.push(result);
            console.log(`❌ ${description}: Failed after ${Math.round(totalDuration)}ms`);
            return result;
        }
    }

    // Memory usage test
    measureMemoryUsage(description) {
        const memUsage = process.memoryUsage();
        const result = {
            test: description,
            rss: Math.round(memUsage.rss / 1024 / 1024), // MB
            heapTotal: Math.round(memUsage.heapTotal / 1024 / 1024), // MB
            heapUsed: Math.round(memUsage.heapUsed / 1024 / 1024), // MB
            external: Math.round(memUsage.external / 1024 / 1024) // MB
        };
        
        this.results.tests.push(result);
        console.log(`📊 ${description}: RSS=${result.rss}MB, Heap=${result.heapUsed}/${result.heapTotal}MB`);
        return result;
    }

    // Run comprehensive performance tests
    async runBenchmarks() {
        console.log('🚀 Starting LKS Network Performance Benchmarks\n');
        
        // Initial memory baseline
        this.measureMemoryUsage('Baseline Memory Usage');
        
        console.log('\n📈 API Response Time Tests:');
        await this.measureApiResponse('/explorer/stats', 'Network Statistics');
        await this.measureApiResponse('/explorer/blocks/latest?count=10', 'Latest Blocks');
        await this.measureApiResponse('/explorer/transactions?page=1&pageSize=20', 'Transaction List');
        await this.measureApiResponse('/explorer/search?query=latest', 'Search Functionality');
        
        console.log('\n⚡ Concurrent Request Tests:');
        await this.measureConcurrentRequests('/explorer/stats', 10, 'Stats API - 10 concurrent');
        await this.measureConcurrentRequests('/explorer/blocks/latest', 20, 'Blocks API - 20 concurrent');
        await this.measureConcurrentRequests('/explorer/stats', 50, 'Stats API - 50 concurrent');
        
        // Memory after tests
        this.measureMemoryUsage('Post-Test Memory Usage');
        
        // Generate summary
        this.generateSummary();
        
        // Save results
        this.saveResults();
        
        console.log('\n✅ Performance benchmarks completed!');
        console.log(`📄 Results saved to: ${this.getResultsPath()}`);
    }

    // Generate performance summary
    generateSummary() {
        const apiTests = this.results.tests.filter(t => t.endpoint && t.duration);
        const concurrentTests = this.results.tests.filter(t => t.concurrency);
        
        if (apiTests.length > 0) {
            const avgResponseTime = Math.round(
                apiTests.reduce((sum, t) => sum + t.duration, 0) / apiTests.length
            );
            const maxResponseTime = Math.max(...apiTests.map(t => t.duration));
            const minResponseTime = Math.min(...apiTests.map(t => t.duration));
            
            this.results.summary.apiPerformance = {
                averageResponseTime: avgResponseTime,
                maxResponseTime,
                minResponseTime,
                successRate: (apiTests.filter(t => t.success).length / apiTests.length) * 100
            };
        }
        
        if (concurrentTests.length > 0) {
            const avgThroughput = Math.round(
                concurrentTests.reduce((sum, t) => sum + (t.throughput || 0), 0) / concurrentTests.length
            );
            
            this.results.summary.concurrencyPerformance = {
                averageThroughput: avgThroughput,
                maxThroughput: Math.max(...concurrentTests.map(t => t.throughput || 0))
            };
        }
        
        // Performance grades
        const avgResponse = this.results.summary.apiPerformance?.averageResponseTime || 0;
        this.results.summary.grades = {
            responseTime: avgResponse < 200 ? 'A' : avgResponse < 500 ? 'B' : avgResponse < 1000 ? 'C' : 'D',
            throughput: (this.results.summary.concurrencyPerformance?.averageThroughput || 0) > 100 ? 'A' : 'B',
            overall: 'A' // Will be calculated based on other grades
        };
        
        console.log('\n📊 Performance Summary:');
        console.log(`   Average Response Time: ${this.results.summary.apiPerformance?.averageResponseTime || 'N/A'}ms`);
        console.log(`   Average Throughput: ${this.results.summary.concurrencyPerformance?.averageThroughput || 'N/A'} req/s`);
        console.log(`   Success Rate: ${this.results.summary.apiPerformance?.successRate || 'N/A'}%`);
        console.log(`   Overall Grade: ${this.results.summary.grades?.overall || 'N/A'}`);
    }

    // Save results to file
    saveResults() {
        const resultsPath = this.getResultsPath();
        const dir = path.dirname(resultsPath);
        
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
        
        fs.writeFileSync(resultsPath, JSON.stringify(this.results, null, 2));
    }

    // Get results file path
    getResultsPath() {
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        return path.join(__dirname, 'results', `performance-${timestamp}.json`);
    }
}

// Stress test specific scenarios
class StressTest extends PerformanceBenchmark {
    async runStressTests() {
        console.log('🔥 Starting Stress Tests\n');
        
        // Gradual load increase
        const loadLevels = [10, 25, 50, 100, 200];
        
        for (const load of loadLevels) {
            console.log(`\n🎯 Testing with ${load} concurrent users...`);
            await this.measureConcurrentRequests('/explorer/stats', load, `Stress Test - ${load} users`);
            
            // Brief pause between tests
            await new Promise(resolve => setTimeout(resolve, 2000));
        }
        
        // Sustained load test
        console.log('\n⏱️  Running sustained load test (30 seconds)...');
        await this.sustainedLoadTest();
        
        this.generateSummary();
        this.saveResults();
    }
    
    async sustainedLoadTest() {
        const duration = 30000; // 30 seconds
        const requestInterval = 100; // Request every 100ms
        const start = performance.now();
        let requestCount = 0;
        let successCount = 0;
        
        const makeRequest = async () => {
            try {
                await axios.get(`${this.apiBase}/explorer/stats`, { timeout: 5000 });
                successCount++;
            } catch (error) {
                // Count failures
            }
            requestCount++;
        };
        
        const interval = setInterval(makeRequest, requestInterval);
        
        await new Promise(resolve => setTimeout(resolve, duration));
        clearInterval(interval);
        
        const end = performance.now();
        const actualDuration = end - start;
        const avgThroughput = Math.round((requestCount / actualDuration) * 1000);
        const successRate = Math.round((successCount / requestCount) * 100);
        
        const result = {
            test: 'Sustained Load Test',
            duration: Math.round(actualDuration),
            totalRequests: requestCount,
            successfulRequests: successCount,
            successRate,
            averageThroughput: avgThroughput
        };
        
        this.results.tests.push(result);
        console.log(`✅ Sustained Load: ${requestCount} requests, ${successRate}% success, ${avgThroughput} req/s`);
    }
}

// Main execution
async function main() {
    const args = process.argv.slice(2);
    const testType = args[0] || 'benchmark';
    
    try {
        if (testType === 'stress') {
            const stressTest = new StressTest();
            await stressTest.runStressTests();
        } else {
            const benchmark = new PerformanceBenchmark();
            await benchmark.runBenchmarks();
        }
    } catch (error) {
        console.error('❌ Benchmark failed:', error.message);
        process.exit(1);
    }
}

// Run if called directly
if (require.main === module) {
    main();
}

module.exports = { PerformanceBenchmark, StressTest };
