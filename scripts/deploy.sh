#!/bin/bash

# File Conversion API Deployment Script
# Supports multiple deployment targets and environments

set -e

# Configuration
APP_NAME="file-conversion-api"
DOCKER_IMAGE="${APP_NAME}:latest"
CONTAINER_NAME="${APP_NAME}-container"
BACKUP_DIR="/opt/${APP_NAME}/backups"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Help function
show_help() {
    cat << EOF
File Conversion API Deployment Script

USAGE:
    $0 [OPTIONS] [COMMAND]

COMMANDS:
    build           Build Docker image
    deploy          Deploy application
    rollback        Rollback to previous version
    status          Show deployment status
    logs            Show application logs
    backup          Create backup of current deployment
    cleanup         Clean up old Docker images and containers

OPTIONS:
    -e, --environment ENV   Deployment environment (development/staging/production) [default: production]
    -t, --tag TAG          Docker image tag [default: latest]
    -p, --port PORT        Port to expose application [default: 3000]
    -h, --help             Show this help message

EXAMPLES:
    $0 build
    $0 -e staging deploy
    $0 -t v1.2.3 deploy
    $0 rollback
    $0 status

EOF
}

# Parse command line arguments
ENVIRONMENT="production"
TAG="latest"
PORT="3000"

while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -t|--tag)
            TAG="$2"
            shift 2
            ;;
        -p|--port)
            PORT="$2"
            shift 2
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            break
            ;;
    esac
done

COMMAND=${1:-"deploy"}

# Environment-specific configuration
case $ENVIRONMENT in
    development)
        COMPOSE_FILE="docker-compose.dev.yml"
        ;;
    staging)
        COMPOSE_FILE="docker-compose.staging.yml"
        ;;
    production)
        COMPOSE_FILE="docker-compose.yml"
        ;;
    *)
        log_error "Unknown environment: $ENVIRONMENT"
        exit 1
        ;;
esac

# Build Docker image
build_image() {
    log_info "Building Docker image: $DOCKER_IMAGE"
    docker build -t "$DOCKER_IMAGE" .

    if [ $? -eq 0 ]; then
        log_success "Docker image built successfully"
    else
        log_error "Failed to build Docker image"
        exit 1
    fi
}

# Deploy application
deploy_app() {
    log_info "Deploying $APP_NAME to $ENVIRONMENT environment"

    # Create backup before deployment
    backup_app

    # Stop existing containers
    log_info "Stopping existing containers..."
    docker-compose -f "$COMPOSE_FILE" down || true

    # Pull latest images if not building locally
    if [ "$TAG" != "latest" ] || [ "$ENVIRONMENT" != "development" ]; then
        log_info "Pulling Docker images..."
        docker-compose -f "$COMPOSE_FILE" pull
    fi

    # Start containers
    log_info "Starting containers..."
    docker-compose -f "$COMPOSE_FILE" up -d

    # Wait for health check
    log_info "Waiting for application to be healthy..."
    max_attempts=30
    attempt=1

    while [ $attempt -le $max_attempts ]; do
        if curl -f http://localhost:$PORT/health > /dev/null 2>&1; then
            log_success "Application is healthy!"
            break
        fi

        log_info "Waiting for health check... (attempt $attempt/$max_attempts)"
        sleep 10
        ((attempt++))
    done

    if [ $attempt -gt $max_attempts ]; then
        log_error "Application failed to become healthy within expected time"
        log_error "Check logs with: $0 logs"
        exit 1
    fi

    log_success "Deployment completed successfully!"
}

# Rollback to previous version
rollback_app() {
    log_info "Rolling back to previous version"

    # Find previous backup
    latest_backup=$(ls -t "$BACKUP_DIR"/*.tar.gz 2>/dev/null | head -1)

    if [ -z "$latest_backup" ]; then
        log_error "No backup found for rollback"
        exit 1
    fi

    log_info "Found backup: $latest_backup"

    # Stop current containers
    docker-compose -f "$COMPOSE_FILE" down

    # Restore from backup
    log_info "Restoring from backup..."
    mkdir -p /tmp/rollback
    tar -xzf "$latest_backup" -C /tmp/rollback

    # Restart with backup
    cd /tmp/rollback
    docker-compose -f "$COMPOSE_FILE" up -d

    log_success "Rollback completed"
}

# Show deployment status
show_status() {
    log_info "Deployment Status for $APP_NAME ($ENVIRONMENT)"

    echo ""
    echo "Docker Containers:"
    docker-compose -f "$COMPOSE_FILE" ps

    echo ""
    echo "Application Health:"
    if curl -f http://localhost:$PORT/health > /dev/null 2>&1; then
        echo -e "${GREEN}✓ Application is healthy${NC}"
    else
        echo -e "${RED}✗ Application is not responding${NC}"
    fi

    echo ""
    echo "Resource Usage:"
    docker stats --no-stream "$CONTAINER_NAME" 2>/dev/null || echo "Container not running"

    echo ""
    echo "Recent Logs:"
    docker-compose -f "$COMPOSE_FILE" logs --tail=10
}

# Show application logs
show_logs() {
    log_info "Application logs for $APP_NAME ($ENVIRONMENT)"
    docker-compose -f "$COMPOSE_FILE" logs -f
}

# Create backup
backup_app() {
    log_info "Creating backup of current deployment"

    mkdir -p "$BACKUP_DIR"

    timestamp=$(date +%Y%m%d_%H%M%S)
    backup_file="$BACKUP_DIR/${APP_NAME}_backup_$timestamp.tar.gz"

    # Backup volumes and configuration
    tar -czf "$backup_file" \
        -C /var/lib/docker/volumes "${APP_NAME}_uploads" \
        -C /var/lib/docker/volumes "${APP_NAME}_converted" \
        -C /var/lib/docker/volumes "${APP_NAME}_logs" \
        docker-compose.yml \
        Dockerfile \
        appsettings.json \
        2>/dev/null || true

    log_success "Backup created: $backup_file"
}

# Clean up old images and containers
cleanup() {
    log_info "Cleaning up old Docker resources"

    # Remove dangling images
    dangling_images=$(docker images -f "dangling=true" -q)
    if [ -n "$dangling_images" ]; then
        log_info "Removing dangling images..."
        echo "$dangling_images" | xargs docker rmi
    fi

    # Remove stopped containers
    stopped_containers=$(docker ps -a -f "status=exited" -q)
    if [ -n "$stopped_containers" ]; then
        log_info "Removing stopped containers..."
        echo "$stopped_containers" | xargs docker rm
    fi

    # Remove unused volumes
    log_info "Removing unused volumes..."
    docker volume prune -f

    log_success "Cleanup completed"
}

# Main execution
case $COMMAND in
    build)
        build_image
        ;;
    deploy)
        deploy_app
        ;;
    rollback)
        rollback_app
        ;;
    status)
        show_status
        ;;
    logs)
        show_logs
        ;;
    backup)
        backup_app
        ;;
    cleanup)
        cleanup
        ;;
    *)
        log_error "Unknown command: $COMMAND"
        show_help
        exit 1
        ;;
esac
