# Deployment Guide

## Prerequisites

- Docker 20.10+
- Docker Compose 2.0+
- Facebook App credentials (App Secret, Page Access Token)

## Environment Variables

Create a `.env` file in the project root:

```env
FACEBOOK_APP_SECRET=your_app_secret_here
FACEBOOK_PAGE_ACCESS_TOKEN=your_page_access_token_here
WEBHOOK_VERIFY_TOKEN=your_verify_token_here
```

## Local Development

### Build and Run

```bash
# Build Docker image
docker build -t messenger-webhook .

# Run with docker-compose
docker-compose up -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

### Health Check

```bash
curl http://localhost:8080/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "channel_queue",
      "status": "Healthy",
      "description": "Queue healthy: 0/1000",
      "data": {
        "queue_depth": 0,
        "capacity": 1000,
        "utilization_percent": 0.0
      }
    }
  ],
  "totalDuration": 15.2
}
```

### Metrics

```bash
curl http://localhost:8080/metrics
```

## Production Deployment

### Using Docker

```bash
# Build production image
docker build -t messenger-webhook:latest .

# Run container
docker run -d \
  --name messenger-webhook \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Facebook__AppSecret=$FACEBOOK_APP_SECRET \
  -e Facebook__PageAccessToken=$FACEBOOK_PAGE_ACCESS_TOKEN \
  -e Webhook__VerifyToken=$WEBHOOK_VERIFY_TOKEN \
  --restart unless-stopped \
  messenger-webhook:latest
```

### Using Docker Compose

```bash
# Start services
docker-compose up -d

# Scale if needed
docker-compose up -d --scale messenger-webhook=3
```

## Facebook Webhook Configuration

1. Go to Facebook App Dashboard
2. Navigate to Webhooks section
3. Add webhook URL: `https://your-domain.com/webhook`
4. Set Verify Token (same as `WEBHOOK_VERIFY_TOKEN`)
5. Subscribe to events: `messages`, `messaging_postbacks`

## Monitoring

### Health Endpoint

- URL: `/health`
- Returns: JSON with service health status
- Checks: Queue depth, Graph API connectivity

### Metrics Endpoint

- URL: `/metrics`
- Returns: Queue metrics (depth, capacity, utilization)

### Logs

View structured JSON logs:
```bash
docker-compose logs -f messenger-webhook
```

## Troubleshooting

### Webhook Verification Failed

- Check `WEBHOOK_VERIFY_TOKEN` matches Facebook configuration
- Verify webhook URL is publicly accessible

### Signature Validation Failed

- Verify `FACEBOOK_APP_SECRET` is correct
- Check request headers contain `X-Hub-Signature-256`

### Queue Full

- Check `/metrics` for queue utilization
- Increase channel capacity in `Program.cs` if needed
- Scale horizontally with multiple instances

## Security Notes

- Never commit `.env` file to git
- Use secrets management in production (Azure Key Vault, AWS Secrets Manager)
- Enable HTTPS in production
- Rotate tokens regularly
