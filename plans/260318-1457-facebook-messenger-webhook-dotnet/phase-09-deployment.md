# Phase 9: Deployment Configuration

## Context Links
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md) - Section: Deployment Considerations

## Overview
- **Priority:** P1 (High)
- **Status:** Pending
- **Mô tả:** Setup Docker, environment configuration, và deployment documentation

## Key Insights
- Multi-stage Docker build giảm image size
- Kestrel behind reverse proxy (Nginx/IIS)
- Azure Container Apps ideal cho webhook workloads
- Health checks cho monitoring
- Environment variables cho secrets

## Requirements

**Functional:**
- Dockerfile với multi-stage build
- Docker Compose cho local development
- Environment variable configuration
- Health check endpoints
- Deployment documentation

**Non-Functional:**
- Docker image < 200MB
- Fast startup time (< 5s)
- Graceful shutdown
- HTTPS enforced

## Architecture

**Deployment Options:**
```
Development: Docker Compose
    ↓
Staging: Azure Container Apps
    ↓
Production: Azure Container Apps + Load Balancer
```

## Related Code Files

**To Create:**
- `Dockerfile`
- `docker-compose.yml`
- `.dockerignore`
- `deployment/azure-container-app.yaml`
- `deployment/nginx.conf`
- `README.md` (deployment section)

## Implementation Steps

1. **Tạo Dockerfile multi-stage**
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj và restore
COPY ["src/MessengerWebhook/MessengerWebhook.csproj", "src/MessengerWebhook/"]
RUN dotnet restore "src/MessengerWebhook/MessengerWebhook.csproj"

# Copy source và build
COPY . .
WORKDIR "/src/src/MessengerWebhook"
RUN dotnet build "MessengerWebhook.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MessengerWebhook.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MessengerWebhook.dll"]
```

2. **Tạo .dockerignore**
```
**/.git
**/.vs
**/.vscode
**/bin
**/obj
**/out
**/.DS_Store
**/node_modules
**/*.user
**/*.suo
**/TestResults
**/.env
**/.env.local
**/appsettings.Development.json
```

3. **Tạo docker-compose.yml**
```yaml
version: '3.8'

services:
  webhook:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - Facebook__AppSecret=${FACEBOOK_APP_SECRET}
      - Facebook__PageAccessToken=${FACEBOOK_PAGE_ACCESS_TOKEN}
      - Webhook__VerifyToken=${WEBHOOK_VERIFY_TOKEN}
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

4. **Tạo .env.example**
```bash
# Facebook Configuration
FACEBOOK_APP_SECRET=your_app_secret_here
FACEBOOK_PAGE_ACCESS_TOKEN=your_page_access_token_here

# Webhook Configuration
WEBHOOK_VERIFY_TOKEN=your_verify_token_here

# Optional: Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING=your_connection_string_here
```

5. **Tạo Azure Container App config**
```yaml
# deployment/azure-container-app.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: messenger-webhook
spec:
  replicas: 2
  selector:
    matchLabels:
      app: messenger-webhook
  template:
    metadata:
      labels:
        app: messenger-webhook
    spec:
      containers:
      - name: webhook
        image: your-registry.azurecr.io/messenger-webhook:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: Facebook__AppSecret
          valueFrom:
            secretKeyRef:
              name: facebook-secrets
              key: app-secret
        - name: Facebook__PageAccessToken
          valueFrom:
            secretKeyRef:
              name: facebook-secrets
              key: page-access-token
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
```

6. **Tạo Nginx reverse proxy config**
```nginx
# deployment/nginx.conf
upstream webhook_backend {
    server localhost:8080;
}

server {
    listen 443 ssl http2;
    server_name webhook.yourdomain.com;

    ssl_certificate /etc/ssl/certs/webhook.crt;
    ssl_certificate_key /etc/ssl/private/webhook.key;

    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass http://webhook_backend;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    location /health {
        proxy_pass http://webhook_backend/health;
        access_log off;
    }
}
```

7. **Update README.md với deployment instructions**
```markdown
## Deployment

### Local Development

1. Copy environment variables:
   ```bash
   cp .env.example .env
   # Edit .env với your credentials
   ```

2. Run với Docker Compose:
   ```bash
   docker-compose up --build
   ```

3. Test webhook verification:
   ```bash
   curl "http://localhost:5000/webhook?hub.mode=subscribe&hub.verify_token=YOUR_TOKEN&hub.challenge=test"
   ```

### Production Deployment (Azure Container Apps)

1. Build và push Docker image:
   ```bash
   docker build -t your-registry.azurecr.io/messenger-webhook:latest .
   docker push your-registry.azurecr.io/messenger-webhook:latest
   ```

2. Create secrets:
   ```bash
   kubectl create secret generic facebook-secrets \
     --from-literal=app-secret=YOUR_APP_SECRET \
     --from-literal=page-access-token=YOUR_PAGE_TOKEN
   ```

3. Deploy:
   ```bash
   kubectl apply -f deployment/azure-container-app.yaml
   ```

4. Configure Facebook webhook:
   - URL: https://your-domain.com/webhook
   - Verify Token: YOUR_VERIFY_TOKEN
   - Subscribe to: messages, messaging_postbacks

### Health Checks

- Health endpoint: `GET /health`
- Metrics endpoint: `GET /metrics`
- Queue depth: `GET /metrics/queue-depth`

### Monitoring

- Application Insights dashboard
- Alert on error rate > 1%
- Alert on queue depth > 1000
- Alert on P95 latency > 5s
```

8. **Test Docker build**
```bash
docker build -t messenger-webhook:test .
docker run -p 5000:8080 --env-file .env messenger-webhook:test
```

## Todo List
- [ ] Tạo Dockerfile multi-stage
- [ ] Tạo .dockerignore
- [ ] Tạo docker-compose.yml
- [ ] Tạo .env.example
- [ ] Tạo Azure Container App config
- [ ] Tạo Nginx config
- [ ] Update README.md với deployment instructions
- [ ] Test Docker build locally
- [ ] Verify health checks work
- [ ] Document environment variables

## Success Criteria
- Docker image builds successfully
- Docker image < 200MB
- Container starts < 5s
- Health checks return 200
- docker-compose up works locally
- README deployment section complete
- All environment variables documented

## Risk Assessment
- **Risk:** Secrets exposed trong Docker image
  - **Mitigation:** Use build args, never COPY .env
- **Risk:** Large Docker image
  - **Mitigation:** Multi-stage build, minimal base image

## Security Considerations
- HTTPS enforced
- Secrets via environment variables
- Non-root user trong container
- Security headers configured
- Rate limiting (implement sau)

## Next Steps
- Deploy to staging environment
- Configure Facebook webhook
- Monitor production metrics
- Setup alerts
