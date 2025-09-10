#!/bin/bash

# LKS Network Production Deployment Script
# This script deploys the complete LKS Network infrastructure

set -e

echo "🚀 Starting LKS Network Production Deployment..."

# Configuration
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_ROOT="$(cd "$DEPLOY_DIR/.." && pwd)"
ENV_FILE="$DEPLOY_DIR/.env.production"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging function
log() {
    echo -e "${GREEN}[$(date +'%Y-%m-%d %H:%M:%S')] $1${NC}"
}

warn() {
    echo -e "${YELLOW}[$(date +'%Y-%m-%d %H:%M:%S')] WARNING: $1${NC}"
}

error() {
    echo -e "${RED}[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $1${NC}"
    exit 1
}

# Check prerequisites
check_prerequisites() {
    log "Checking prerequisites..."
    
    # Check if Docker is installed and running
    if ! command -v docker &> /dev/null; then
        error "Docker is not installed. Please install Docker first."
    fi
    
    if ! docker info &> /dev/null; then
        error "Docker is not running. Please start Docker first."
    fi
    
    # Check if Docker Compose is available
    if ! command -v docker-compose &> /dev/null; then
        error "Docker Compose is not installed. Please install Docker Compose first."
    fi
    
    # Check if environment file exists
    if [ ! -f "$ENV_FILE" ]; then
        warn "Environment file not found. Creating from example..."
        cp "$DEPLOY_DIR/env.production.example" "$ENV_FILE"
        error "Please configure $ENV_FILE with your production values before deploying."
    fi
    
    log "Prerequisites check passed ✓"
}

# Build Docker images
build_images() {
    log "Building Docker images..."
    
    # Build LKS Node
    log "Building LKS Network Node..."
    docker build -t lks-network/node:latest "$PROJECT_ROOT/src/LksBrothers.Node" || error "Failed to build LKS Node"
    
    # Build API Server
    log "Building API Server..."
    docker build -f "$PROJECT_ROOT/Dockerfile.api" -t lks-network/api:latest "$PROJECT_ROOT" || error "Failed to build API Server"
    
    # Build Explorer
    log "Building Explorer..."
    docker build -t lks-network/explorer:latest "$PROJECT_ROOT/src/LksBrothers.Explorer" || error "Failed to build Explorer"
    
    log "Docker images built successfully ✓"
}

# Initialize database
init_database() {
    log "Initializing database..."
    
    # Start database container temporarily
    docker-compose -f "$DEPLOY_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE" up -d lks-database
    
    # Wait for database to be ready
    log "Waiting for database to be ready..."
    sleep 30
    
    # Run database schema
    docker exec lks-database /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$(grep DB_PASSWORD $ENV_FILE | cut -d'=' -f2)" -i /docker-entrypoint-initdb.d/schema.sql
    
    log "Database initialized ✓"
}

# Deploy infrastructure
deploy_infrastructure() {
    log "Deploying LKS Network infrastructure..."
    
    # Pull latest images
    docker-compose -f "$DEPLOY_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE" pull
    
    # Start all services
    docker-compose -f "$DEPLOY_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE" up -d
    
    log "Infrastructure deployed ✓"
}

# Health checks
run_health_checks() {
    log "Running health checks..."
    
    # Wait for services to start
    sleep 60
    
    # Check LKS Node
    if curl -f http://localhost:8545 &> /dev/null; then
        log "LKS Node health check passed ✓"
    else
        warn "LKS Node health check failed"
    fi
    
    # Check API Server
    if curl -f http://localhost:3000/health &> /dev/null; then
        log "API Server health check passed ✓"
    else
        warn "API Server health check failed"
    fi
    
    # Check Explorer
    if curl -f http://localhost:5000/health &> /dev/null; then
        log "Explorer health check passed ✓"
    else
        warn "Explorer health check failed"
    fi
    
    # Check Database
    if docker exec lks-database /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$(grep DB_PASSWORD $ENV_FILE | cut -d'=' -f2)" -Q "SELECT 1" &> /dev/null; then
        log "Database health check passed ✓"
    else
        warn "Database health check failed"
    fi
}

# Setup SSL certificates
setup_ssl() {
    log "Setting up SSL certificates..."
    
    # Create SSL directory
    mkdir -p "$DEPLOY_DIR/nginx/ssl"
    
    # Generate self-signed certificates for development
    if [ ! -f "$DEPLOY_DIR/nginx/ssl/lks-network.crt" ]; then
        warn "Generating self-signed SSL certificate for development..."
        openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
            -keyout "$DEPLOY_DIR/nginx/ssl/lks-network.key" \
            -out "$DEPLOY_DIR/nginx/ssl/lks-network.crt" \
            -subj "/C=US/ST=CA/L=San Francisco/O=LKS Brothers/CN=lks-network.com"
        warn "For production, replace with real SSL certificates from a CA"
    fi
    
    log "SSL certificates configured ✓"
}

# Setup monitoring
setup_monitoring() {
    log "Setting up monitoring..."
    
    # Create monitoring directories
    mkdir -p "$DEPLOY_DIR/monitoring/grafana/dashboards"
    mkdir -p "$DEPLOY_DIR/monitoring/grafana/datasources"
    
    # Create Prometheus config
    cat > "$DEPLOY_DIR/monitoring/prometheus.yml" << EOF
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'lks-node'
    static_configs:
      - targets: ['lks-node:8545']
  
  - job_name: 'lks-api'
    static_configs:
      - targets: ['lks-api:3000']
  
  - job_name: 'lks-explorer'
    static_configs:
      - targets: ['lks-explorer:80']
EOF
    
    log "Monitoring configured ✓"
}

# Backup setup
setup_backup() {
    log "Setting up backup system..."
    
    # Create backup script
    cat > "$DEPLOY_DIR/scripts/backup.sh" << 'EOF'
#!/bin/bash
BACKUP_DIR="/backups/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

# Backup database
docker exec lks-database /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -Q "BACKUP DATABASE LKSNetwork TO DISK = '/tmp/lksnetwork.bak'"
docker cp lks-database:/tmp/lksnetwork.bak "$BACKUP_DIR/"

# Backup blockchain data
docker cp lks-node:/data "$BACKUP_DIR/blockchain-data"

echo "Backup completed: $BACKUP_DIR"
EOF
    
    chmod +x "$DEPLOY_DIR/scripts/backup.sh"
    
    log "Backup system configured ✓"
}

# Main deployment function
main() {
    log "🦁 LKS Network Production Deployment Started"
    
    check_prerequisites
    setup_ssl
    build_images
    init_database
    setup_monitoring
    setup_backup
    deploy_infrastructure
    run_health_checks
    
    log "🎉 LKS Network Production Deployment Completed Successfully!"
    log ""
    log "📊 Services Status:"
    log "   • LKS Network Node: http://localhost:8545"
    log "   • API Server: http://localhost:3000"
    log "   • Explorer: http://localhost:5000"
    log "   • Database: localhost:1433"
    log "   • Monitoring: http://localhost:3001 (Grafana)"
    log "   • Metrics: http://localhost:9090 (Prometheus)"
    log ""
    log "🔐 Default Credentials:"
    log "   • Database: sa / (check .env.production)"
    log "   • Grafana: admin / (check .env.production)"
    log ""
    log "📝 Next Steps:"
    log "   1. Configure your domain DNS to point to this server"
    log "   2. Replace self-signed SSL certificates with real ones"
    log "   3. Configure external API keys in .env.production"
    log "   4. Set up automated backups"
    log "   5. Configure monitoring alerts"
    log ""
    log "🚀 LKS Network is now ready for production!"
}

# Run main function
main "$@"
