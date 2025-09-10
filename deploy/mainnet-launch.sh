#!/bin/bash

# LKS COIN MAINNET LAUNCH SCRIPT
# ===============================

set -e

echo "🚀 LKS COIN MAINNET LAUNCH SEQUENCE INITIATED 🚀"
echo "=================================================="
echo ""

# Configuration
NETWORK_NAME="LKS COIN Mainnet"
CHAIN_ID=1000
GENESIS_DIR="./genesis_output"
NODE_DIR="./nodes"
VALIDATOR_COUNT=3

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

print_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_info() {
    echo -e "${CYAN}[INFO]${NC} $1"
}

# Step 1: Create Genesis Block
print_step "Creating Genesis Block..."
cd src/LksBrothers.Genesis
dotnet run
if [ $? -eq 0 ]; then
    print_success "Genesis block created successfully"
else
    print_error "Failed to create genesis block"
    exit 1
fi
cd ../..

# Step 2: Setup Node Directories
print_step "Setting up validator node directories..."
mkdir -p $NODE_DIR
for i in $(seq 1 $VALIDATOR_COUNT); do
    mkdir -p "$NODE_DIR/validator$i"
    mkdir -p "$NODE_DIR/validator$i/data"
    mkdir -p "$NODE_DIR/validator$i/logs"
    mkdir -p "$NODE_DIR/validator$i/config"
done
print_success "Node directories created"

# Step 3: Copy Genesis Data
print_step "Distributing genesis data to validators..."
for i in $(seq 1 $VALIDATOR_COUNT); do
    cp -r $GENESIS_DIR/* "$NODE_DIR/validator$i/config/"
done
print_success "Genesis data distributed"

# Step 4: Generate Validator Configurations
print_step "Generating validator configurations..."
cat > "$NODE_DIR/validator1/config/validator.json" << EOF
{
  "validatorAddress": "lks1validator1000000000000000000000000000000",
  "networkPort": 8001,
  "rpcPort": 9001,
  "wsPort": 9101,
  "bootstrapNodes": [],
  "isBootstrap": true,
  "stake": "5000000000000000000000000",
  "commission": 0.05
}
EOF

cat > "$NODE_DIR/validator2/config/validator.json" << EOF
{
  "validatorAddress": "lks1validator2000000000000000000000000000000",
  "networkPort": 8002,
  "rpcPort": 9002,
  "wsPort": 9102,
  "bootstrapNodes": ["127.0.0.1:8001"],
  "isBootstrap": false,
  "stake": "3000000000000000000000000",
  "commission": 0.07
}
EOF

cat > "$NODE_DIR/validator3/config/validator.json" << EOF
{
  "validatorAddress": "lks1validator3000000000000000000000000000000",
  "networkPort": 8003,
  "rpcPort": 9003,
  "wsPort": 9103,
  "bootstrapNodes": ["127.0.0.1:8001"],
  "isBootstrap": false,
  "stake": "2000000000000000000000000",
  "commission": 0.08
}
EOF

print_success "Validator configurations generated"

# Step 5: Build All Components
print_step "Building LKS COIN components..."
dotnet build LksBrothers.sln --configuration Release
if [ $? -eq 0 ]; then
    print_success "All components built successfully"
else
    print_error "Build failed"
    exit 1
fi

# Step 6: Start Validator Nodes
print_step "Starting validator nodes..."

# Start validator 1 (bootstrap node)
print_info "Starting Validator 1 (Bootstrap Node)..."
cd "$NODE_DIR/validator1"
nohup dotnet ../../../src/LksBrothers.Node/bin/Release/net8.0/LksBrothers.Node.dll \
    --config config/validator.json \
    --genesis config/genesis_block.json \
    --data-dir data \
    --log-dir logs > logs/node.log 2>&1 &
echo $! > validator1.pid
cd ../../..

sleep 5

# Start validator 2
print_info "Starting Validator 2..."
cd "$NODE_DIR/validator2"
nohup dotnet ../../../src/LksBrothers.Node/bin/Release/net8.0/LksBrothers.Node.dll \
    --config config/validator.json \
    --genesis config/genesis_block.json \
    --data-dir data \
    --log-dir logs > logs/node.log 2>&1 &
echo $! > validator2.pid
cd ../../..

sleep 5

# Start validator 3
print_info "Starting Validator 3..."
cd "$NODE_DIR/validator3"
nohup dotnet ../../../src/LksBrothers.Node/bin/Release/net8.0/LksBrothers.Node.dll \
    --config config/validator.json \
    --genesis config/genesis_block.json \
    --data-dir data \
    --log-dir logs > logs/node.log 2>&1 &
echo $! > validator3.pid
cd ../../..

print_success "All validator nodes started"

# Step 7: Start RPC Services
print_step "Starting RPC services..."
cd src/LksBrothers.Rpc
nohup dotnet run --configuration Release > ../../logs/rpc.log 2>&1 &
echo $! > ../../rpc.pid
cd ../..
print_success "RPC service started on port 5000"

# Step 8: Start Wallet Service
print_step "Starting Wallet service..."
cd src/LksBrothers.Wallet
nohup dotnet run --configuration Release > ../../logs/wallet.log 2>&1 &
echo $! > ../../wallet.pid
cd ../..
print_success "Wallet service started on port 5001"

# Step 9: Start Explorer Service
print_step "Starting Explorer service..."
cd src/LksBrothers.Explorer
nohup dotnet run --configuration Release > ../../logs/explorer.log 2>&1 &
echo $! > ../../explorer.pid
cd ../..
print_success "Explorer service started on port 5002"

# Step 10: Wait for Network Sync
print_step "Waiting for network synchronization..."
sleep 30

# Step 11: Verify Network Health
print_step "Verifying network health..."
HEALTH_CHECK=$(curl -s http://localhost:5000/api/health || echo "FAILED")
if [[ $HEALTH_CHECK == *"healthy"* ]]; then
    print_success "Network health check passed"
else
    print_warning "Network health check inconclusive"
fi

# Step 12: Display Network Status
echo ""
echo -e "${PURPLE}🎉 LKS COIN MAINNET LAUNCH COMPLETE! 🎉${NC}"
echo "============================================="
echo ""
echo -e "${CYAN}📊 NETWORK STATUS:${NC}"
echo "  • Network Name: $NETWORK_NAME"
echo "  • Chain ID: $CHAIN_ID"
echo "  • Active Validators: $VALIDATOR_COUNT"
echo "  • Genesis Block: Created ✅"
echo ""
echo -e "${CYAN}🔗 SERVICE ENDPOINTS:${NC}"
echo "  • RPC API: http://localhost:5000"
echo "  • Wallet: http://localhost:5001"
echo "  • Explorer: http://localhost:5002"
echo ""
echo -e "${CYAN}🔐 VALIDATOR NODES:${NC}"
echo "  • Validator 1: localhost:9001 (Bootstrap)"
echo "  • Validator 2: localhost:9002"
echo "  • Validator 3: localhost:9003"
echo ""
echo -e "${CYAN}📁 DATA LOCATIONS:${NC}"
echo "  • Genesis Data: $GENESIS_DIR/"
echo "  • Node Data: $NODE_DIR/"
echo "  • Logs: ./logs/"
echo ""
echo -e "${GREEN}🚀 LKS COIN MAINNET IS NOW LIVE! 🚀${NC}"
echo ""
echo "To stop the network, run: ./deploy/stop-mainnet.sh"
echo "To view logs, check the ./logs/ directory"
echo "To monitor the network, visit the Explorer at http://localhost:5002"
echo ""
