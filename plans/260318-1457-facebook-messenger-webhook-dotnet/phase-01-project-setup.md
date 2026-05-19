# Phase 1: Project Setup

## Context Links
- [.NET Webhook Implementation](../reports/researcher-260318-1431-dotnet-webhook-implementation.md)

## Overview
- **Priority:** P0 (Critical)
- **Status:** Completed
- **Mô tả:** Khởi tạo ASP.NET Core 8 Minimal API project với dependencies và configuration

## Key Insights
- Minimal API nhanh hơn 30% so với MVC Controllers
- User Secrets cho dev, Environment Variables cho production
- Strongly-typed configuration với IOptions pattern

## Requirements

**Functional:**
- Tạo solution với 3 projects (main, unit tests, integration tests)
- Cài đặt Polly v8, Moq, FluentAssertions
- Setup configuration hierarchy
- Configure structured logging

**Non-Functional:**
- Tuân thủ .NET 8 best practices
- Separation of concerns
- High testability

## Architecture

**Tech Stack:**
- .NET 8 SDK
- ASP.NET Core 8 Minimal API
- System.Threading.Channels
- Polly v8
- xUnit + Moq

**Configuration Layers:**
1. appsettings.json (defaults)
2. User Secrets (dev)
3. Environment Variables (production)

## Related Code Files

**To Create:**
- `MessengerWebhook.sln`
- `src/MessengerWebhook/Program.cs`
- `src/MessengerWebhook/MessengerWebhook.csproj`
- `src/MessengerWebhook/appsettings.json`
- `src/MessengerWebhook/Configuration/FacebookOptions.cs`
- `src/MessengerWebhook/Configuration/WebhookOptions.cs`
- `tests/MessengerWebhook.UnitTests/MessengerWebhook.UnitTests.csproj`
- `tests/MessengerWebhook.IntegrationTests/MessengerWebhook.IntegrationTests.csproj`
- `Dockerfile`

## Implementation Steps

1. **Tạo solution và projects**
```bash
dotnet new sln -n MessengerWebhook
dotnet new web -n MessengerWebhook -o src/MessengerWebhook
dotnet new xunit -n MessengerWebhook.UnitTests -o tests/MessengerWebhook.UnitTests
dotnet new xunit -n MessengerWebhook.IntegrationTests -o tests/MessengerWebhook.IntegrationTests
dotnet sln add src/MessengerWebhook/MessengerWebhook.csproj
dotnet sln add tests/MessengerWebhook.UnitTests/MessengerWebhook.UnitTests.csproj
dotnet sln add tests/MessengerWebhook.IntegrationTests/MessengerWebhook.IntegrationTests.csproj
```

2. **Cài đặt NuGet packages**
```bash
cd src/MessengerWebhook
dotnet add package Polly --version 8.0.0
dotnet add package Microsoft.Extensions.Http.Polly --version 8.0.0

cd ../../tests/MessengerWebhook.UnitTests
dotnet add reference ../../src/MessengerWebhook/MessengerWebhook.csproj
dotnet add package Moq --version 4.20.0
dotnet add package FluentAssertions --version 6.12.0

cd ../MessengerWebhook.IntegrationTests
dotnet add reference ../../src/MessengerWebhook/MessengerWebhook.csproj
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 8.0.0
```

3. **Tạo cấu trúc thư mục**
```bash
mkdir -p src/MessengerWebhook/{Services,Models,Configuration,BackgroundServices,Middleware}
```

4. **Tạo Configuration classes**
- FacebookOptions: AppSecret, PageAccessToken, ApiVersion, GraphApiBaseUrl
- WebhookOptions: VerifyToken, TimeoutSeconds, MaxRetries, ChannelCapacity

5. **Setup appsettings.json**
- Logging configuration
- Webhook settings (timeout, retries, channel capacity)
- Facebook API settings (version, base URL)

6. **Setup User Secrets**
```bash
dotnet user-secrets init
dotnet user-secrets set "Facebook:AppSecret" "dev_secret"
dotnet user-secrets set "Facebook:PageAccessToken" "dev_token"
dotnet user-secrets set "Webhook:VerifyToken" "dev_verify_token"
```

7. **Setup Program.cs**
- Configure services (IOptions, HttpClient, Logging)
- Register configuration classes
- Add health check endpoint

8. **Tạo Dockerfile multi-stage**
- Build stage với SDK image
- Runtime stage với aspnet image
- Expose port 8080

9. **Verify build**
```bash
dotnet build
dotnet test
```

## Todo List
- [x] Tạo solution và 3 projects
- [x] Cài đặt dependencies (Polly, Moq, FluentAssertions)
- [x] Tạo cấu trúc thư mục
- [x] Viết FacebookOptions và WebhookOptions
- [x] Setup appsettings.json
- [x] Setup User Secrets
- [x] Viết Program.cs cơ bản
- [x] Tạo Dockerfile
- [x] Verify build thành công

## Success Criteria
- `dotnet build` thành công
- `dotnet test` chạy được
- Docker build thành công
- Configuration load đúng
- Health check `/health` trả về 200

## Risk Assessment
- **Risk:** Polly v7/v8 API conflict
  - **Mitigation:** Dùng Polly v8 với ResiliencePipeline API
- **Risk:** Docker build chậm
  - **Mitigation:** Multi-stage build với layer caching

## Security Considerations
- Không commit secrets vào git
- User Secrets chỉ cho development
- Production dùng Environment Variables
- .gitignore exclude sensitive files

## Next Steps
- Phase 2: Webhook verification endpoint
