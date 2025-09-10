#!/bin/bash

# LKS Network Deployment Script
# Usage: ./deploy.sh [environment] [options]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="development"
SCALE_NODES=3
FORCE_RECREATE=false
BACKUP_BEFORE_DEPLOY=true
HEALTH_CHECK_TIMEOUT=300

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
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

# Function to show usage
show_usage() {
    echo "Usage: $0 [environment] [options]"
    echo ""
    echo "Environments:"
    echo "  development  - Local development deployment"
    echo "  staging      - Staging environment deployment"
    echo "  production   - Production environment deployment"
    echo ""
    echo "Options:"
    echo "  --scale N           Scale to N nodes (default: 3)"
    echo "  --force-recreate    Force recreate all containers"
    echo "  --no-backup         Skip backup before deployment"
    echo "  --timeout N         Health check timeout in seconds (default: 300)"
    echo "  --help              Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 development --scale 1"
    echo "  $0 production --scale 5 --force-recreate"
    echo "  $0 staging --no-backup --timeout 600"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        development|staging|production)
            ENVIRONMENT="$1"
            shift
            ;;
        --scale)
            SCALE_NODES="$2"
            shift 2
            ;;
        --force-recreate)
            FORCE_RECREATE=true
            shift
            ;;
        --no-backup)
            BACKUP_BEFORE_DEPLOY=false
            shift
            ;;
        --timeout)
            HEALTH_CHECK_TIMEOUT="$2"
            shift 2
            ;;
        --help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

print_status "Starting LKS Network deployment for environment: $ENVIRONMENT"

# Set environment-specific configurations
case $ENVIRONMENT in
    development)
        COMPOSE_FILE="docker-compose.dev.yml"
        DOMAIN="localhost"
        ;;
    staging)
        COMPOSE_FILE="docker-compose.staging.yml"
        DOMAIN="staging.lksnetwork.com"
        ;;
    production)
        COMPOSE_FILE="docker-compose.yml"
        DOMAIN="lksnetwork.com"
        ;;
esac

# Check prerequisites
check_prerequisites() {
    print_status "Checking prerequisites..."
    
    # Check if Docker is installed and running
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed"
        exit 1
    fi
    
    if ! docker info &> /dev/null; then
        print_error "Docker daemon is not running"
        exit 1
    fi
    
    # Check if Docker Compose is available
    if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
        print_error "Docker Compose is not installed"
        exit 1
    fi
    
    # Check if required files exist
    if [[ ! -f "$COMPOSE_FILE" ]]; then
        print_error "Docker Compose file not found: $COMPOSE_FILE"
        exit 1
    fi
    
    print_success "Prerequisites check passed"
}

# Backup function
backup_data() {
    if [[ "$BACKUP_BEFORE_DEPLOY" == true ]]; then
        print_status "Creating backup..."
        
        BACKUP_DIR="./backups/$(date +%Y%m%d_%H%M%S)"
        mkdir -p "$BACKUP_DIR"
        
        # Backup Redis data
        if docker ps | grep -q lks-redis; then
            print_status "Backing up Redis data..."
            docker exec lks-redis redis-cli BGSAVE
            docker cp lks-redis:/data/dump.rdb "$BACKUP_DIR/redis_dump.rdb"
        fi
        
        # Backup node data
        if [[ -d "./data" ]]; then
            print_status "Backing up node data..."
            cp -r ./data "$BACKUP_DIR/"
        fi
        
        # Backup configuration
        if [[ -d "./config" ]]; then
            print_status "Backing up configuration..."
            cp -r ./config "$BACKUP_DIR/"
        fi
        
        print_success "Backup created at: $BACKUP_DIR"
    fi
}

# Build and deploy function
deploy_services() {
    print_status "Building and deploying services..."
    
    # Set Docker Compose command
    if command -v docker-compose &> /dev/null; then
        COMPOSE_CMD="docker-compose"
    else
        COMPOSE_CMD="docker compose"
    fi
    
    # Build images
    print_status "Building Docker images..."
    $COMPOSE_CMD -f "$COMPOSE_FILE" build
    
    # Deploy services
    if [[ "$FORCE_RECREATE" == true ]]; then
        print_status "Force recreating all services..."
        $COMPOSE_CMD -f "$COMPOSE_FILE" up -d --force-recreate
    else
        print_status "Starting services..."
        $COMPOSE_CMD -f "$COMPOSE_FILE" up -d
    fi
    
    # Scale nodes if specified
    if [[ "$SCALE_NODES" -gt 1 ]]; then
        print_status "Scaling to $SCALE_NODES nodes..."
        $COMPOSE_CMD -f "$COMPOSE_FILE" up -d --scale lks-node="$SCALE_NODES"
    fi
    
    print_success "Services deployed successfully"
}

# Health check function
perform_health_checks() {
    print_status "Performing health checks..."
    
    local timeout=$HEALTH_CHECK_TIMEOUT
    local elapsed=0
    local interval=10
    
    while [[ $elapsed -lt $timeout ]]; do
        local healthy=true
        
        # Check LKS Node
        if ! curl -f -s "http://localhost:8080/health" > /dev/null; then
            healthy=false
            print_warning "LKS Node health check failed"
        fi
        
        # Check Explorer
        if ! curl -f -s "http://localhost:5000/health" > /dev/null; then
            healthy=false
            print_warning "Explorer health check failed"
        fi
        
        # Check Admin Dashboard
        if ! curl -f -s "http://localhost:5001/health" > /dev/null; then
            healthy=false
            print_warning "Admin Dashboard health check failed"
        fi
        
        # Check Redis
        if ! docker exec lks-redis redis-cli ping > /dev/null 2>&1; then
            healthy=false
            print_warning "Redis health check failed"
        fi
        
        if [[ "$healthy" == true ]]; then
            print_success "All health checks passed"
            return 0
        fi
        
        print_status "Waiting for services to be ready... ($elapsed/$timeout seconds)"
        sleep $interval
        elapsed=$((elapsed + interval))
    done
    
    print_error "Health checks failed after $timeout seconds"
    return 1
}

# Show deployment status
show_status() {
    print_status "Deployment Status:"
    echo ""
    
    # Show running containers
    if command -v docker-compose &> /dev/null; then
        docker-compose -f "$COMPOSE_FILE" ps
    else
        docker compose -f "$COMPOSE_FILE" ps
    fi
    
    echo ""
    print_status "Service URLs:"
    echo "  Explorer:        http://$DOMAIN:5000"
    echo "  Admin Dashboard: http://$DOMAIN:5001"
    echo "  Grafana:         http://$DOMAIN:3000 (admin/lks_admin_2024)"
    echo "  Prometheus:      http://$DOMAIN:9090"
    echo "  Kibana:          http://$DOMAIN:5601"
    echo ""
    
    # Show resource usage
    print_status "Resource Usage:"
    docker stats --no-stream --format "table {{.Container}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}" | head -10
}

# Rollback function
rollback() {
    print_warning "Rolling back deployment..."
    
    if command -v docker-compose &> /dev/null; then
        docker-compose -f "$COMPOSE_FILE" down
    else
        docker compose -f "$COMPOSE_FILE" down
    fi
    
    # Restore from latest backup if available
    LATEST_BACKUP=$(ls -t ./backups/ | head -1)
    if [[ -n "$LATEST_BACKUP" ]]; then
        print_status "Restoring from backup: $LATEST_BACKUP"
        
        # Restore Redis data
        if [[ -f "./backups/$LATEST_BACKUP/redis_dump.rdb" ]]; then
            docker run --rm -v "$(pwd)/backups/$LATEST_BACKUP:/backup" -v redis_data:/data alpine cp /backup/redis_dump.rdb /data/
        fi
        
        # Restore node data
        if [[ -d "./backups/$LATEST_BACKUP/data" ]]; then
            rm -rf ./data
            cp -r "./backups/$LATEST_BACKUP/data" ./
        fi
        
        # Restore configuration
        if [[ -d "./backups/$LATEST_BACKUP/config" ]]; then
            rm -rf ./config
            cp -r "./backups/$LATEST_BACKUP/config" ./
        fi
    fi
    
    print_success "Rollback completed"
}

# Cleanup function
cleanup() {
    print_status "Cleaning up..."
    
    # Remove unused images
    docker image prune -f
    
    # Remove unused volumes (be careful in production)
    if [[ "$ENVIRONMENT" == "development" ]]; then
        docker volume prune -f
    fi
    
    print_success "Cleanup completed"
}

# Main deployment flow
main() {
    print_status "LKS Network Deployment Script"
    print_status "Environment: $ENVIRONMENT"
    print_status "Scale: $SCALE_NODES nodes"
    print_status "Force Recreate: $FORCE_RECREATE"
    print_status "Backup: $BACKUP_BEFORE_DEPLOY"
    echo ""
    
    # Trap to handle rollback on failure
    trap 'print_error "Deployment failed. Rolling back..."; rollback; exit 1' ERR
    
    check_prerequisites
    backup_data
    deploy_services
    
    if perform_health_checks; then
        show_status
        print_success "🎉 LKS Network deployment completed successfully!"
        
        if [[ "$ENVIRONMENT" == "production" ]]; then
            print_status "Production deployment checklist:"
            echo "  ✅ Services are running"
            echo "  ✅ Health checks passed"
            echo "  ⚠️  Monitor logs for any issues"
            echo "  ⚠️  Verify SSL certificates"
            echo "  ⚠️  Check monitoring dashboards"
        fi
    else
        print_error "Deployment failed health checks"
        rollback
        exit 1
    fi
    
    # Optional cleanup
    if [[ "$ENVIRONMENT" == "development" ]]; then
        read -p "Do you want to clean up unused Docker resources? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            cleanup
        fi
    fi
}

# Run main function
main "$@"
