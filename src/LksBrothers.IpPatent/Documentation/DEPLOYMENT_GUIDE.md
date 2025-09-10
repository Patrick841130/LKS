# LKS Brothers IP PATENT System - Deployment Guide

## Prerequisites

### System Requirements
- **Operating System**: Linux (Ubuntu 20.04+ recommended) or Windows Server 2019+
- **Runtime**: .NET 8.0 Runtime
- **Database**: PostgreSQL 15+ or SQL Server 2019+
- **Cache**: Redis 7.0+
- **Web Server**: Nginx or IIS
- **Memory**: Minimum 8GB RAM (16GB+ recommended for production)
- **Storage**: Minimum 100GB SSD (500GB+ recommended for production)

### Dependencies
```bash
# Ubuntu/Debian
sudo apt update
sudo apt install -y nginx postgresql redis-server

# Install .NET 8.0
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

## Environment Configuration

### 1. Database Setup

#### PostgreSQL Configuration
```sql
-- Create database and user
CREATE DATABASE lks_ip_patent;
CREATE USER lks_patent_user WITH PASSWORD 'secure_password_here';
GRANT ALL PRIVILEGES ON DATABASE lks_ip_patent TO lks_patent_user;

-- Connect to the database
\c lks_ip_patent

-- Create extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
```

#### Connection String
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=lks_ip_patent;Username=lks_patent_user;Password=secure_password_here;SSL Mode=Require;"
  }
}
```

### 2. Redis Configuration

#### Redis Setup
```bash
# Configure Redis
sudo nano /etc/redis/redis.conf

# Key settings:
bind 127.0.0.1
port 6379
requirepass your_redis_password
maxmemory 2gb
maxmemory-policy allkeys-lru

# Restart Redis
sudo systemctl restart redis-server
sudo systemctl enable redis-server
```

### 3. Application Configuration

#### appsettings.Production.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "LksBrothers.IpPatent": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=lks_ip_patent;Username=lks_patent_user;Password=secure_password_here;SSL Mode=Require;",
    "Redis": "localhost:6379,password=your_redis_password"
  },
  "Security": {
    "RateLimiting": {
      "Enabled": true,
      "DefaultMaxRequests": 1000,
      "DefaultTimeWindow": "01:00:00",
      "BlockDuration": "00:15:00"
    },
    "IpFiltering": {
      "Enabled": true,
      "WhitelistedIps": ["127.0.0.1", "::1"],
      "BlacklistedIps": []
    },
    "SecurityHeaders": {
      "Enabled": true,
      "Hsts": {
        "Enabled": true,
        "MaxAgeSeconds": 31536000,
        "IncludeSubDomains": true
      }
    }
  },
  "Email": {
    "Smtp": {
      "Host": "smtp.sendgrid.net",
      "Port": 587,
      "Username": "apikey",
      "Password": "your_sendgrid_api_key",
      "EnableSsl": true,
      "FromEmail": "noreply@lksnetwork.io",
      "FromName": "LKS Brothers IP PATENT"
    }
  },
  "Backup": {
    "Enabled": true,
    "BackupDirectory": "/var/backups/lks-ip-patent",
    "RetentionDays": 30,
    "ScheduleInterval": "24:00:00",
    "AdminNotificationEmails": ["admin@lksnetwork.io"]
  },
  "Blockchain": {
    "Networks": {
      "Ethereum": {
        "RpcUrl": "https://mainnet.infura.io/v3/your_project_id",
        "ContractAddress": "0x1234567890123456789012345678901234567890",
        "PrivateKey": "your_private_key_here"
      },
      "Polygon": {
        "RpcUrl": "https://polygon-rpc.com",
        "ContractAddress": "0x1234567890123456789012345678901234567890",
        "PrivateKey": "your_private_key_here"
      }
    },
    "IPFS": {
      "ApiUrl": "https://ipfs.infura.io:5001",
      "ProjectId": "your_ipfs_project_id",
      "ProjectSecret": "your_ipfs_project_secret"
    }
  },
  "Monitoring": {
    "ApplicationInsights": {
      "ConnectionString": "your_app_insights_connection_string"
    },
    "Serilog": {
      "MinimumLevel": "Information",
      "WriteTo": [
        {
          "Name": "Console"
        },
        {
          "Name": "File",
          "Args": {
            "path": "/var/log/lks-ip-patent/app-.log",
            "rollingInterval": "Day",
            "retainedFileCountLimit": 30
          }
        }
      ]
    }
  }
}
```

## Deployment Steps

### 1. Build and Package

#### Local Build
```bash
# Clone repository
git clone https://github.com/lks-brothers/ip-patent-system.git
cd ip-patent-system/src/LksBrothers.IpPatent

# Restore dependencies
dotnet restore

# Build for production
dotnet publish -c Release -o ./publish --self-contained false

# Create deployment package
tar -czf lks-ip-patent-v1.2.0.tar.gz -C publish .
```

#### CI/CD Pipeline (GitHub Actions)
```yaml
name: Deploy to Production

on:
  push:
    branches: [main]
    tags: ['v*']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
          
      - name: Restore dependencies
        run: dotnet restore
        
      - name: Build
        run: dotnet build --no-restore -c Release
        
      - name: Test
        run: dotnet test --no-build --verbosity normal
        
      - name: Publish
        run: dotnet publish -c Release -o ./publish
        
      - name: Deploy to server
        uses: appleboy/ssh-action@v0.1.5
        with:
          host: ${{ secrets.HOST }}
          username: ${{ secrets.USERNAME }}
          key: ${{ secrets.SSH_KEY }}
          script: |
            sudo systemctl stop lks-ip-patent
            sudo rm -rf /opt/lks-ip-patent/backup
            sudo mv /opt/lks-ip-patent /opt/lks-ip-patent/backup
            sudo mkdir -p /opt/lks-ip-patent
            
      - name: Copy files
        uses: appleboy/scp-action@v0.1.4
        with:
          host: ${{ secrets.HOST }}
          username: ${{ secrets.USERNAME }}
          key: ${{ secrets.SSH_KEY }}
          source: "./publish/*"
          target: "/opt/lks-ip-patent/"
          
      - name: Start service
        uses: appleboy/ssh-action@v0.1.5
        with:
          host: ${{ secrets.HOST }}
          username: ${{ secrets.USERNAME }}
          key: ${{ secrets.SSH_KEY }}
          script: |
            sudo chown -R lks-patent:lks-patent /opt/lks-ip-patent
            sudo systemctl start lks-ip-patent
            sudo systemctl status lks-ip-patent
```

### 2. Server Setup

#### Create Application User
```bash
# Create dedicated user
sudo useradd -r -s /bin/false lks-patent
sudo mkdir -p /opt/lks-ip-patent
sudo chown lks-patent:lks-patent /opt/lks-ip-patent

# Create log directory
sudo mkdir -p /var/log/lks-ip-patent
sudo chown lks-patent:lks-patent /var/log/lks-ip-patent

# Create backup directory
sudo mkdir -p /var/backups/lks-ip-patent
sudo chown lks-patent:lks-patent /var/backups/lks-ip-patent
```

#### Systemd Service Configuration
```bash
# Create service file
sudo nano /etc/systemd/system/lks-ip-patent.service
```

```ini
[Unit]
Description=LKS Brothers IP PATENT System
After=network.target postgresql.service redis.service

[Service]
Type=notify
User=lks-patent
Group=lks-patent
WorkingDirectory=/opt/lks-ip-patent
ExecStart=/opt/lks-ip-patent/LksBrothers.IpPatent
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=lks-ip-patent
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

# Security settings
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/var/log/lks-ip-patent /var/backups/lks-ip-patent /tmp
ProtectHome=true
ProtectKernelTunables=true
ProtectKernelModules=true
ProtectControlGroups=true

[Install]
WantedBy=multi-user.target
```

#### Enable and Start Service
```bash
# Reload systemd and enable service
sudo systemctl daemon-reload
sudo systemctl enable lks-ip-patent
sudo systemctl start lks-ip-patent

# Check status
sudo systemctl status lks-ip-patent
sudo journalctl -u lks-ip-patent -f
```

### 3. Nginx Configuration

#### SSL Certificate Setup
```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx

# Obtain SSL certificate
sudo certbot --nginx -d api.lksnetwork.io
```

#### Nginx Virtual Host
```bash
sudo nano /etc/nginx/sites-available/lks-ip-patent
```

```nginx
upstream lks_ip_patent {
    server 127.0.0.1:5000;
    keepalive 32;
}

server {
    listen 80;
    server_name api.lksnetwork.io;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.lksnetwork.io;

    # SSL Configuration
    ssl_certificate /etc/letsencrypt/live/api.lksnetwork.io/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/api.lksnetwork.io/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-RSA-AES256-GCM-SHA512:DHE-RSA-AES256-GCM-SHA512:ECDHE-RSA-AES256-GCM-SHA384:DHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    # Security Headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options nosniff always;
    add_header X-Frame-Options DENY always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;

    # Rate Limiting
    limit_req_zone $binary_remote_addr zone=api:10m rate=10r/s;
    limit_req zone=api burst=20 nodelay;

    # File Upload Limits
    client_max_body_size 100M;
    client_body_timeout 60s;
    client_header_timeout 60s;

    # Proxy Settings
    proxy_buffering on;
    proxy_buffer_size 128k;
    proxy_buffers 4 256k;
    proxy_busy_buffers_size 256k;

    location / {
        proxy_pass http://lks_ip_patent;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # SignalR WebSocket Support
    location /hubs/ {
        proxy_pass http://lks_ip_patent;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache off;
    }

    # Static Files (if serving directly)
    location /static/ {
        alias /opt/lks-ip-patent/wwwroot/;
        expires 1y;
        add_header Cache-Control "public, immutable";
        gzip_static on;
    }

    # Health Check Endpoint
    location /health {
        access_log off;
        proxy_pass http://lks_ip_patent;
    }
}
```

#### Enable Site
```bash
# Enable site and restart Nginx
sudo ln -s /etc/nginx/sites-available/lks-ip-patent /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx
```

### 4. Database Migration

#### Run Migrations
```bash
# Navigate to application directory
cd /opt/lks-ip-patent

# Run database migrations
sudo -u lks-patent dotnet LksBrothers.IpPatent.dll --migrate-database

# Seed initial data (if needed)
sudo -u lks-patent dotnet LksBrothers.IpPatent.dll --seed-data
```

## Monitoring and Maintenance

### 1. Health Checks

#### Application Health Endpoint
```bash
# Check application health
curl -f https://api.lksnetwork.io/health

# Detailed health check
curl -f https://api.lksnetwork.io/health/detailed
```

#### System Health Script
```bash
#!/bin/bash
# /opt/scripts/health-check.sh

echo "=== LKS IP PATENT System Health Check ==="
echo "Date: $(date)"
echo

# Check service status
echo "Service Status:"
systemctl is-active lks-ip-patent
echo

# Check database connectivity
echo "Database Status:"
sudo -u lks-patent psql -h localhost -U lks_patent_user -d lks_ip_patent -c "SELECT 1;" > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "Database: OK"
else
    echo "Database: ERROR"
fi

# Check Redis connectivity
echo "Redis Status:"
redis-cli -a your_redis_password ping > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo "Redis: OK"
else
    echo "Redis: ERROR"
fi

# Check disk space
echo "Disk Usage:"
df -h /opt/lks-ip-patent /var/log/lks-ip-patent /var/backups/lks-ip-patent

# Check memory usage
echo "Memory Usage:"
free -h

# Check application logs for errors
echo "Recent Errors:"
journalctl -u lks-ip-patent --since "1 hour ago" --grep "ERROR|FATAL" --no-pager -q
```

### 2. Backup Verification

#### Backup Test Script
```bash
#!/bin/bash
# /opt/scripts/backup-test.sh

BACKUP_DIR="/var/backups/lks-ip-patent"
TEST_RESTORE_DIR="/tmp/backup-test"

echo "=== Backup Verification ==="

# Find latest backup
LATEST_BACKUP=$(ls -t $BACKUP_DIR/full_*.zip | head -n1)

if [ -z "$LATEST_BACKUP" ]; then
    echo "ERROR: No backup files found"
    exit 1
fi

echo "Testing backup: $LATEST_BACKUP"

# Create test directory
mkdir -p $TEST_RESTORE_DIR
cd $TEST_RESTORE_DIR

# Extract backup
unzip -q "$LATEST_BACKUP"

# Verify database backup
if [ -f "database.sql" ]; then
    echo "Database backup: OK"
else
    echo "Database backup: MISSING"
fi

# Verify file backup
if [ -d "files" ]; then
    echo "File backup: OK"
else
    echo "File backup: MISSING"
fi

# Cleanup
rm -rf $TEST_RESTORE_DIR

echo "Backup verification completed"
```

### 3. Log Rotation

#### Logrotate Configuration
```bash
sudo nano /etc/logrotate.d/lks-ip-patent
```

```
/var/log/lks-ip-patent/*.log {
    daily
    missingok
    rotate 30
    compress
    delaycompress
    notifempty
    create 644 lks-patent lks-patent
    postrotate
        systemctl reload lks-ip-patent
    endscript
}
```

### 4. Automated Monitoring

#### Cron Jobs
```bash
# Edit crontab for monitoring user
sudo crontab -e

# Add monitoring jobs
# Health check every 5 minutes
*/5 * * * * /opt/scripts/health-check.sh >> /var/log/health-check.log 2>&1

# Backup verification daily at 2 AM
0 2 * * * /opt/scripts/backup-test.sh >> /var/log/backup-test.log 2>&1

# Cleanup old logs weekly
0 3 * * 0 find /var/log/lks-ip-patent -name "*.log" -mtime +30 -delete
```

## Security Hardening

### 1. Firewall Configuration

#### UFW Setup
```bash
# Enable UFW
sudo ufw enable

# Allow SSH (adjust port as needed)
sudo ufw allow 22/tcp

# Allow HTTP/HTTPS
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp

# Allow database (only from application server)
sudo ufw allow from 127.0.0.1 to any port 5432

# Allow Redis (only from application server)
sudo ufw allow from 127.0.0.1 to any port 6379

# Check status
sudo ufw status verbose
```

### 2. Fail2Ban Configuration

#### Install and Configure Fail2Ban
```bash
# Install Fail2Ban
sudo apt install fail2ban

# Create custom jail
sudo nano /etc/fail2ban/jail.local
```

```ini
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5

[nginx-http-auth]
enabled = true
filter = nginx-http-auth
logpath = /var/log/nginx/error.log

[nginx-limit-req]
enabled = true
filter = nginx-limit-req
logpath = /var/log/nginx/error.log
maxretry = 10

[lks-ip-patent]
enabled = true
filter = lks-ip-patent
logpath = /var/log/lks-ip-patent/app-*.log
maxretry = 5
```

## Troubleshooting

### Common Issues

#### 1. Application Won't Start
```bash
# Check service status
sudo systemctl status lks-ip-patent

# Check logs
sudo journalctl -u lks-ip-patent -n 50

# Check configuration
sudo -u lks-patent dotnet LksBrothers.IpPatent.dll --validate-config
```

#### 2. Database Connection Issues
```bash
# Test database connection
sudo -u lks-patent psql -h localhost -U lks_patent_user -d lks_ip_patent

# Check PostgreSQL status
sudo systemctl status postgresql

# Check connection limits
sudo -u postgres psql -c "SELECT * FROM pg_stat_activity;"
```

#### 3. High Memory Usage
```bash
# Check memory usage
free -h
ps aux --sort=-%mem | head

# Check application memory
sudo systemctl show lks-ip-patent --property=MemoryCurrent

# Restart if needed
sudo systemctl restart lks-ip-patent
```

### Performance Tuning

#### Database Optimization
```sql
-- PostgreSQL configuration tuning
-- Add to postgresql.conf

shared_buffers = 256MB
effective_cache_size = 1GB
maintenance_work_mem = 64MB
checkpoint_completion_target = 0.9
wal_buffers = 16MB
default_statistics_target = 100
random_page_cost = 1.1
effective_io_concurrency = 200
```

#### Application Tuning
```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 1000,
      "MaxConcurrentUpgradedConnections": 100,
      "MaxRequestBodySize": 104857600,
      "KeepAliveTimeout": "00:02:00",
      "RequestHeadersTimeout": "00:00:30"
    }
  }
}
```

This deployment guide provides comprehensive instructions for setting up the LKS Brothers IP PATENT System in a production environment with proper security, monitoring, and maintenance procedures.
