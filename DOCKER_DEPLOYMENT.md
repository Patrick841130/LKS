# LKS COIN Docker Deployment Guide

## Quick Start with Docker

Since .NET SDK is not installed on your system, you can use Docker to run the entire LKS COIN mainnet without installing .NET dependencies.

### Prerequisites
- Docker Desktop installed
- Docker Compose available
- 8GB+ RAM available
- 50GB+ disk space

### Installation Steps

#### 1. Install Docker Desktop (if not installed)
```bash
# macOS with Homebrew
brew install --cask docker

# Or download from: https://www.docker.com/products/docker-desktop/
```

#### 2. Verify Docker Installation
```bash
docker --version
docker-compose --version
```

#### 3. Launch LKS COIN Mainnet
```bash
cd /Users/liphopcharles/Development/lks-brothers-mainnet

# Build and start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Check service status
docker-compose ps
```

#### 4. Access Services
- **Demo Explorer**: Open `demo-explorer.html` in browser
- **Block Explorer API**: http://localhost:8080
- **RPC Endpoint**: http://localhost:8545
- **Wallet Interface**: http://localhost:3000
- **Monitoring (Grafana)**: http://localhost:3001 (admin/lksadmin)
- **Metrics (Prometheus)**: http://localhost:9090

#### 5. Stop Services
```bash
docker-compose down

# Remove all data (fresh start)
docker-compose down -v
```

## Service Architecture

### Core Blockchain Services
- **Genesis Creator**: Initializes blockchain state
- **Validator Network**: 3-node consensus cluster
- **RPC Node**: Public API endpoint
- **Explorer API**: Blockchain data indexing

### Supporting Services
- **PostgreSQL**: Explorer database
- **Nginx**: Load balancer and reverse proxy
- **Prometheus**: Metrics collection
- **Grafana**: Monitoring dashboards

### Network Configuration
- **Internal Network**: 172.20.0.0/16
- **Service Discovery**: Docker DNS
- **Load Balancing**: Nginx upstream

## Health Monitoring

### Check Service Health
```bash
# All services status
docker-compose ps

# Individual service logs
docker-compose logs validator-1
docker-compose logs rpc-node
docker-compose logs explorer-api

# Resource usage
docker stats
```

### API Health Checks
```bash
# RPC endpoint
curl http://localhost:8545/health

# Explorer API
curl http://localhost:8080/api/health

# Validator status
curl http://localhost:8001/status
```

## Development Mode

### Hot Reload Development
```bash
# Start with development overrides
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up

# Rebuild specific service
docker-compose build validator-1
docker-compose up -d validator-1
```

### Debug Mode
```bash
# Enable debug logging
export ASPNETCORE_ENVIRONMENT=Development
docker-compose up -d

# Attach to running container
docker exec -it lks-validator-1 bash
```

## Production Deployment

### Environment Variables
```bash
# Create production environment file
cat > .env.production << EOF
ASPNETCORE_ENVIRONMENT=Production
POSTGRES_PASSWORD=secure_password_here
GRAFANA_ADMIN_PASSWORD=secure_admin_password
SSL_CERT_PATH=/etc/nginx/ssl/cert.pem
SSL_KEY_PATH=/etc/nginx/ssl/key.pem
EOF
```

### SSL Configuration
```bash
# Generate self-signed certificates (development)
mkdir -p nginx/ssl
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout nginx/ssl/key.pem \
  -out nginx/ssl/cert.pem \
  -subj "/C=US/ST=CA/L=SF/O=LKS Brothers/CN=localhost"
```

### Backup and Recovery
```bash
# Backup blockchain data
docker run --rm -v lks-brothers-mainnet_postgres_data:/data \
  -v $(pwd)/backups:/backup alpine \
  tar czf /backup/postgres-$(date +%Y%m%d).tar.gz /data

# Backup validator state
docker-compose exec validator-1 tar czf /app/data/backup.tar.gz /app/data/state
```

## Troubleshooting

### Common Issues

#### Port Conflicts
```bash
# Check what's using ports
lsof -i :8080 -i :8545 -i :3000

# Use different ports
export RPC_PORT=8546
export EXPLORER_PORT=8081
docker-compose up -d
```

#### Memory Issues
```bash
# Increase Docker memory limit in Docker Desktop settings
# Or use resource limits in docker-compose.yml

# Check container resource usage
docker stats --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}"
```

#### Database Connection Issues
```bash
# Reset database
docker-compose stop explorer-db
docker volume rm lks-brothers-mainnet_postgres_data
docker-compose up -d explorer-db

# Check database logs
docker-compose logs explorer-db
```

### Performance Tuning

#### Resource Limits
```yaml
# Add to docker-compose.yml services
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 4G
    reservations:
      cpus: '1.0'
      memory: 2G
```

#### Volume Optimization
```bash
# Use named volumes for better performance
volumes:
  validator_data:
    driver: local
    driver_opts:
      type: none
      o: bind
      device: /fast/ssd/path
```

## Scaling

### Horizontal Scaling
```bash
# Scale RPC nodes
docker-compose up -d --scale rpc-node=3

# Scale validators (requires configuration changes)
docker-compose up -d --scale validator=5
```

### Load Testing
```bash
# Install load testing tools
npm install -g artillery

# Run load test
artillery quick --count 100 --num 10 http://localhost:8545/api/blocks
```

## Security

### Network Security
- All internal communication uses Docker network
- Only necessary ports exposed to host
- Nginx handles SSL termination
- Rate limiting configured

### Container Security
- Non-root user in containers
- Read-only file systems where possible
- Security scanning with Docker Scout
- Regular base image updates

---

**Ready to Launch**: Your LKS COIN mainnet is now containerized and ready for deployment!
