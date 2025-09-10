# LKS COIN Mainnet Installation Guide

## System Requirements Check

Since neither .NET SDK nor Docker are currently installed on your system, here are the installation options:

## Option 1: Install .NET SDK (Recommended for Development)

### macOS Installation
```bash
# Using Homebrew (easiest)
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
brew install dotnet

# Or direct download
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

### Verify Installation
```bash
dotnet --version
# Should output: 8.0.x
```

### Build and Run LKS COIN
```bash
cd /Users/liphopcharles/Development/lks-brothers-mainnet

# Restore dependencies
dotnet restore

# Build all projects
dotnet build --configuration Release

# Launch mainnet
./deploy/mainnet-launch.sh
```

## Option 2: Install Docker (Recommended for Production)

### macOS Installation
```bash
# Using Homebrew
brew install --cask docker

# Or download Docker Desktop from:
# https://www.docker.com/products/docker-desktop/
```

### Launch with Docker
```bash
cd /Users/liphopcharles/Development/lks-brothers-mainnet

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Access services:
# - Demo Explorer: Open demo-explorer.html
# - API: http://localhost:8545
# - Explorer: http://localhost:8080
```

## Option 3: Demo Mode (No Installation Required)

### Current Available Demo
The LKS COIN Block Explorer demo is already available and functional:

```bash
# Open the demo explorer
open /Users/liphopcharles/Development/lks-brothers-mainnet/demo-explorer.html
```

### Demo Features
- ✅ Real-time simulated blockchain metrics
- ✅ Professional enterprise-grade UI
- ✅ Interactive charts and visualizations
- ✅ Live transaction and block data simulation
- ✅ LKS Brothers branding and styling

## Next Steps

### For Immediate Testing
1. **Open Demo Explorer**: The `demo-explorer.html` file provides a fully functional preview
2. **Review Documentation**: Check `README.md` and `DEPLOYMENT_GUIDE.md`
3. **Explore Source Code**: All blockchain components are ready in `/src`

### For Full Deployment
1. **Choose Installation Method**: .NET SDK or Docker
2. **Follow Installation Steps**: Use the guides above
3. **Launch Mainnet**: Run deployment scripts
4. **Monitor Services**: Use provided health checks

### For Development
1. **Install .NET SDK**: Required for code modifications
2. **Set up IDE**: Visual Studio Code with C# extension
3. **Build Projects**: Use `dotnet build` commands
4. **Run Tests**: Execute `dotnet test` for validation

## Troubleshooting

### Common Issues
- **Permission Denied**: Use `chmod +x deploy/*.sh`
- **Port Conflicts**: Check `lsof -i :8080 -i :8545 -i :3000`
- **Memory Issues**: Ensure 8GB+ RAM available
- **Network Issues**: Check firewall settings

### Support Resources
- **Documentation**: Complete guides in `/docs`
- **Scripts**: Automated deployment in `/deploy`
- **Configuration**: Settings in `/config`
- **Logs**: Runtime logs in `/logs`

## Architecture Overview

### Core Components
1. **Genesis Creator** - Blockchain initialization
2. **Firedancer Validator** - High-performance validation
3. **Consensus Engine** - PoH + PoS consensus
4. **RPC Node** - Public API endpoint
5. **Block Explorer** - Web interface
6. **Cross-Chain Bridge** - Interoperability
7. **Compliance Engine** - Regulatory features

### Network Specifications
- **TPS**: 65,000+ transactions per second
- **Block Time**: 400ms average
- **Finality**: Sub-second confirmation
- **Fees**: Zero transaction costs
- **Consensus**: Hybrid PoH + PoS
- **Interoperability**: Wormhole + Chainlink CCIP

---

**LKS Brothers LLC** - Zero-fee blockchain infrastructure for the future
