#!/bin/bash

# LKS COIN MAINNET STOP SCRIPT
# =============================

set -e

echo "🛑 STOPPING LKS COIN MAINNET..."
echo "================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
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

# Stop services
print_step "Stopping services..."

# Stop RPC service
if [ -f "rpc.pid" ]; then
    PID=$(cat rpc.pid)
    if kill -0 $PID 2>/dev/null; then
        kill $PID
        print_success "RPC service stopped"
    fi
    rm -f rpc.pid
fi

# Stop Wallet service
if [ -f "wallet.pid" ]; then
    PID=$(cat wallet.pid)
    if kill -0 $PID 2>/dev/null; then
        kill $PID
        print_success "Wallet service stopped"
    fi
    rm -f wallet.pid
fi

# Stop Explorer service
if [ -f "explorer.pid" ]; then
    PID=$(cat explorer.pid)
    if kill -0 $PID 2>/dev/null; then
        kill $PID
        print_success "Explorer service stopped"
    fi
    rm -f explorer.pid
fi

# Stop validator nodes
print_step "Stopping validator nodes..."

for i in {1..3}; do
    PID_FILE="nodes/validator$i/validator$i.pid"
    if [ -f "$PID_FILE" ]; then
        PID=$(cat "$PID_FILE")
        if kill -0 $PID 2>/dev/null; then
            kill $PID
            print_success "Validator $i stopped"
        fi
        rm -f "$PID_FILE"
    fi
done

echo ""
echo -e "${GREEN}✅ LKS COIN MAINNET STOPPED SUCCESSFULLY${NC}"
echo ""
