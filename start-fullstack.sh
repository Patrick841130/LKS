#!/bin/bash

# LKS NETWORK Full Stack Startup Script
echo "🦁 Starting LKS NETWORK Full Stack Explorer..."

# Check if .env exists
if [ ! -f .env ]; then
    echo "⚠️  Creating .env from .env.example..."
    cp .env.example .env
    echo "✅ Please update .env with your actual configuration values"
fi

# Start Node.js Payment Backend
echo "🚀 Starting Payment Backend..."
npm start &
PAYMENT_PID=$!

# Wait for payment backend to be ready
echo "⏳ Waiting for payment backend to start..."
sleep 5

# Check if payment backend is running
if curl -f http://localhost:3000/health > /dev/null 2>&1; then
    echo "✅ Payment Backend is running on port 3000"
else
    echo "❌ Payment Backend failed to start"
    kill $PAYMENT_PID 2>/dev/null
    exit 1
fi

# Start ASP.NET Core Explorer (if built)
if [ -d "src/LksBrothers.Explorer/bin" ]; then
    echo "🚀 Starting Explorer Backend..."
    cd src/LksBrothers.Explorer
    dotnet run &
    EXPLORER_PID=$!
    cd ../..
    
    echo "⏳ Waiting for explorer backend to start..."
    sleep 10
    
    if curl -f http://localhost:5000/health > /dev/null 2>&1; then
        echo "✅ Explorer Backend is running on port 5000"
    else
        echo "⚠️  Explorer Backend may not be ready yet"
    fi
fi

# Start simple HTTP server for frontend
echo "🌐 Starting Frontend Server..."
python3 -m http.server 8080 &
FRONTEND_PID=$!

echo ""
echo "🎉 LKS NETWORK Full Stack is now running!"
echo ""
echo "📱 Frontend:        http://localhost:8080"
echo "💳 Payment API:     http://localhost:3000"
echo "🔍 Explorer API:    http://localhost:5000"
echo ""
echo "🦁 LKS NETWORK Logo: Displayed prominently in hero section"
echo "💰 XRP Payments:    Available via 'Send XRP' button"
echo "🔐 Authentication:  Login modal with email/wallet options"
echo ""
echo "Press Ctrl+C to stop all services..."

# Function to cleanup on exit
cleanup() {
    echo ""
    echo "🛑 Stopping all services..."
    kill $PAYMENT_PID 2>/dev/null
    kill $EXPLORER_PID 2>/dev/null
    kill $FRONTEND_PID 2>/dev/null
    echo "✅ All services stopped"
    exit 0
}

# Set trap for cleanup
trap cleanup SIGINT SIGTERM

# Wait for user to stop
wait
