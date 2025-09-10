# LKS COIN Mainnet Deployment Guide

## Prerequisites

### System Requirements
- **Operating System**: macOS, Linux, or Windows
- **.NET 8.0 SDK**: Required for running blockchain services
- **Memory**: Minimum 8GB RAM (16GB recommended)
- **Storage**: 100GB+ available space for blockchain data
- **Network**: Stable internet connection with open ports

### Installing .NET 8.0 SDK

#### macOS (Homebrew)
```bash
brew install dotnet
```

#### macOS (Direct Download)
```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
export PATH="$HOME/.dotnet:$PATH"
```

#### Verify Installation
```bash
dotnet --version
# Should output: 8.0.x
```

## Quick Start

### 1. Clone and Setup
```bash
git clone <repository-url>
cd lks-brothers-mainnet
chmod +x deploy/*.sh
```

### 2. Build All Services
```bash
dotnet restore
dotnet build --configuration Release
```

### 3. Launch Mainnet
```bash
./deploy/mainnet-launch.sh
```

### 4. Access Services
- **Block Explorer**: http://localhost:8080
- **RPC API**: http://localhost:8545
- **Wallet Interface**: http://localhost:3000
- **Demo Explorer**: Open `demo-explorer.html` in browser

### 5. Stop Mainnet
```bash
./deploy/stop-mainnet.sh
```

## Service Architecture

### Core Services
1. **Genesis Creator** (`LksBrothers.Genesis`)
   - Creates initial blockchain state
   - Distributes 50B LKS tokens
   - Configures validator network

2. **Firedancer Validator** (`LksBrothers.Firedancer`)
   - High-performance transaction validation
   - Parallel processing engine
   - PoH sequence verification

3. **Consensus Engine** (`LksBrothers.Consensus`)
   - Hybrid PoH + PoS consensus
   - 400ms block time target
   - Byzantine fault tolerance

4. **Cross-Chain Bridge** (`LksBrothers.CrossChain`)
   - Wormhole integration
   - Chainlink CCIP support
   - Multi-chain interoperability

5. **Compliance Engine** (`LksBrothers.Compliance`)
   - KYC/AML screening
   - Sanctions list checking
   - Regulatory reporting

## Network Configuration

### Validator Network
- **Validator 1**: Primary consensus leader
- **Validator 2**: Secondary consensus participant  
- **Validator 3**: Backup consensus participant
- **RPC Node**: Public API endpoint
- **Archive Node**: Historical data storage

### Performance Targets
- **TPS**: 65,000+ transactions per second
- **Block Time**: 400ms average
- **Finality**: Sub-second confirmation
- **Fees**: Zero transaction fees (foundation sponsored)

## Monitoring and Maintenance

### Health Checks
```bash
# Check validator status
curl http://localhost:8545/health

# Check consensus state
curl http://localhost:8545/consensus/status

# Check network metrics
curl http://localhost:8545/metrics
```

### Log Files
- Genesis: `logs/genesis.log`
- Validators: `logs/validator-{1,2,3}.log`
- RPC: `logs/rpc.log`
- Explorer: `logs/explorer.log`

### Backup Procedures
```bash
# Backup blockchain data
tar -czf backup-$(date +%Y%m%d).tar.gz data/

# Backup configuration
cp -r config/ config-backup-$(date +%Y%m%d)/
```

## Troubleshooting

### Common Issues

#### Port Conflicts
```bash
# Check port usage
lsof -i :8080 -i :8545 -i :3000

# Kill conflicting processes
sudo kill -9 $(lsof -t -i:8080)
```

#### Memory Issues
```bash
# Increase swap space (Linux)
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
```

#### Validator Sync Issues
```bash
# Reset validator state
rm -rf data/validator-*/
./deploy/mainnet-launch.sh --reset
```

### Performance Tuning

#### System Optimizations
```bash
# Increase file descriptor limits
ulimit -n 65536

# Optimize network buffers
sudo sysctl -w net.core.rmem_max=134217728
sudo sysctl -w net.core.wmem_max=134217728
```

#### Application Settings
```json
{
  "Performance": {
    "MaxConcurrentValidations": 1000,
    "BatchSize": 10000,
    "CacheSize": "2GB",
    "EnableSIMD": true
  }
}
```

## Security Considerations

### Network Security
- Use firewall to restrict access to validator ports
- Enable TLS for all external communications
- Implement rate limiting on RPC endpoints

### Key Management
- Store validator keys in secure hardware modules
- Use multi-signature schemes for critical operations
- Regular key rotation for non-validator services

### Monitoring
- Set up alerts for unusual network activity
- Monitor validator performance metrics
- Track consensus participation rates

## Production Deployment

### Infrastructure Requirements
- **Load Balancer**: For RPC endpoint distribution
- **Database**: PostgreSQL for explorer data
- **Monitoring**: Prometheus + Grafana stack
- **Logging**: ELK stack for centralized logging

### Scaling Considerations
- Horizontal scaling of RPC nodes
- Database sharding for explorer data
- CDN for static explorer assets
- Geographic distribution of validators

## Support and Resources

### Documentation
- [API Reference](./docs/API.md)
- [Architecture Overview](./docs/ARCHITECTURE.md)
- [Developer Guide](./docs/DEVELOPMENT.md)

### Community
- **Discord**: [LKS Brothers Community](https://discord.gg/lksbrothers)
- **GitHub**: [Issues and Discussions](https://github.com/lks-brothers/mainnet)
- **Email**: support@lksnetwork.io

### Emergency Contacts
- **Technical Issues**: tech@lksnetwork.io
- **Security Issues**: security@lksnetwork.io
- **Business Inquiries**: business@lksnetwork.io

---

**LKS Brothers LLC** - Building the future of zero-fee blockchain infrastructure
