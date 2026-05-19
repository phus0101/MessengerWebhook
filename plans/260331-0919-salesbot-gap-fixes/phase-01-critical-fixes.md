# Phase 1: Critical Fixes

**Duration:** 1-2 days (16h)
**Cost:** 12M VND
**Status:** ✅ Completed
**Priority:** P0 - CRITICAL

---

## Overview

Fix 3 critical gaps:
1. Email notification với SMTP và HTML templates
2. Risk message không để lộ đánh giá khách hàng
3. System prompt aggressive hơn về "ép chốt"

---

## Task 1.1: Email Notification Enhancement (6h)

### Current State
- File: `src/MessengerWebhook/Services/Support/EmailNotificationService.cs`
- Basic SMTP exists (lines 22-61)
- Plain text only, no HTML, no action buttons

### Implementation

**1. Create Email Template Service (2h)**

Files to create:
- `Services/Support/EmailTemplates/IEmailTemplateService.cs`
- `Services/Support/EmailTemplates/EmailTemplateService.cs`
- `Services/Support/EmailTemplates/SupportCaseAssignedTemplate.cs`

```csharp
// IEmailTemplateService.cs
public interface IEmailTemplateService
{
    string GenerateSupportCaseAssignedEmail(
        Guid caseId,
        string customerPSID,
        string reason,
        string summary,
        DateTime createdAt,
        string completeUrl,
        string dashboardUrl);
}

// EmailTemplateService.cs
public class EmailTemplateService : IEmailTemplateService
{
    public string GenerateSupportCaseAssignedEmail(...)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; background: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 20px auto; background: white; }}
        .header {{ background: #4267B2; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 20px; }}
        .info-box {{ background: #f8f9fa; padding: 15px; margin: 15px 0; border-left: 4px solid #4267B2; }}
        .button {{ display: inline-block; background: #4267B2; color: white; padding: 12px 24px;
                  text-decoration: none; border-radius: 4px; margin: 10px 5px; }}
        .button:hover {{ background: #365899; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h2>🔔 Support Case Assigned</h2>
        </div>
        <div class=""content"">
            <p>Xin chào,</p>
            <p>Một khách hàng cần hỗ trợ. Vui lòng xem xét và xử lý case này.</p>

            <div class=""info-box"">
                <strong>Case ID:</strong> {caseId}<br>
                <strong>Customer PSID:</strong> {customerPSID}<br>
                <strong>Reason:</strong> {reason}<br>
                <strong>Summary:</strong> {summary}<br>
                <strong>Created:</strong> {createdAt:yyyy-MM-dd HH:mm:ss}
            </div>

            <div style=""text-align: center; margin: 20px 0;"">
                <a href=""{completeUrl}"" class=""button"">✓ Complete Case</a>
                <a href=""{dashboardUrl}"" class=""button"">📊 View Dashboard</a>
            </div>

            <p style=""color: #666; font-size: 12px;"">
                Clicking ""Complete Case"" will unlock the bot and allow it to resume conversations with this customer.
            </p>
        </div>
    </div>
</body>
</html>";
    }
}
```

**2. Enhance EmailNotificationService (2h)**

Update `Services/Support/EmailNotificationService.cs`:

```csharp
public class EmailNotificationService : IEmailNotificationService
{
    private readonly IEmailTemplateService _templateService;
    private readonly ISupportCaseTokenService _tokenService;
    private readonly EmailOptions _options;

    public async Task SendSupportCaseAssignedAsync(
        HumanSupportCase supportCase,
        CancellationToken cancellationToken = default)
    {
        var token = _tokenService.GenerateToken(supportCase.Id);
        var completeUrl = $"{_options.BaseUrl}/internal/support-cases/{supportCase.Id}/complete?token={token}&source=email";
        var dashboardUrl = $"{_options.BaseUrl}/admin/support-cases/{supportCase.Id}";

        var htmlBody = _templateService.GenerateSupportCaseAssignedEmail(
            supportCase.Id,
            supportCase.FacebookPSID,
            supportCase.Reason.ToString(),
            supportCase.Summary,
            supportCase.CreatedAt,
            completeUrl,
            dashboardUrl);

        var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = $"Support Case Assigned: {supportCase.Id}",
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(supportCase.AssignedToEmail);

        using var smtp = new SmtpClient(_options.Host, _options.Port)
        {
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };

        await smtp.SendMailAsync(message, cancellationToken);
    }
}
```

**3. Configuration Updates (1h)**

Update `Configuration/EmailOptions.cs`:

```csharp
public class EmailOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty; // NEW
    public bool EnableHtmlEmails { get; set; } = true; // NEW
}
```

Update `appsettings.json`:

```json
{
  "Email": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromAddress": "noreply@muixu.com",
    "FromName": "Múi Xù Support",
    "EnableSsl": true,
    "BaseUrl": "https://your-domain.com",
    "EnableHtmlEmails": true
  }
}
```

**4. Service Registration (1h)**

Update `Program.cs`:

```csharp
// Add after line 145
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<ISupportCaseTokenService, SupportCaseTokenService>();
```

---

## Task 1.2: Unlock Endpoint Enhancement (4h)

### Current State
- File: `src/MessengerWebhook/Endpoints/InternalOperationsEndpointExtensions.cs`
- POST endpoint exists (line 64-84): `/internal/support-cases/{id}/complete`
- Returns JSON, not user-friendly

### Implementation

**1. Create Token Service (2h)**

Files to create:
- `Services/Support/ISupportCaseTokenService.cs`
- `Services/Support/SupportCaseTokenService.cs`

```csharp
// ISupportCaseTokenService.cs
public interface ISupportCaseTokenService
{
    string GenerateToken(Guid caseId);
    bool ValidateToken(Guid caseId, string token);
}

// SupportCaseTokenService.cs
public class SupportCaseTokenService : ISupportCaseTokenService
{
    private readonly SupportOptions _options;
    private readonly ILogger<SupportCaseTokenService> _logger;

    public string GenerateToken(Guid caseId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = $"{caseId}:{timestamp}";
        var signature = ComputeHmac(data);
        return $"{data}:{signature}";
    }

    public bool ValidateToken(Guid caseId, string token)
    {
        var parts = token.Split(':');
        if (parts.Length != 3) return false;

        var tokenCaseId = Guid.Parse(parts[0]);
        var timestamp = long.Parse(parts[1]);
        var signature = parts[2];

        // Validate case ID
        if (tokenCaseId != caseId) return false;

        // Validate expiration (7 days)
        var expirationSeconds = _options.TokenExpirationDays * 24 * 60 * 60;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - timestamp > expirationSeconds) return false;

        // Validate signature
        var data = $"{parts[0]}:{parts[1]}";
        var expectedSignature = ComputeHmac(data);
        return signature == expectedSignature;
    }

    private string ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.TokenSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }
}
```

**2. Add GET Endpoint (1h)**

Update `Endpoints/InternalOperationsEndpointExtensions.cs`:

```csharp
// Add after line 84
group.MapGet("/support-cases/{id:guid}/complete", async (
    Guid id,
    string? token,
    string? source,
    MessengerBotDbContext dbContext,
    IBotLockService botLockService,
    ISupportCaseTokenService tokenService,
    ILogger<InternalOperationsEndpointExtensions> logger,
    CancellationToken cancellationToken) =>
{
    // Validate token
    if (string.IsNullOrWhiteSpace(token) || !tokenService.ValidateToken(id, token))
    {
        return Results.Content(GenerateErrorHtml("Invalid or expired link"), "text/html");
    }

    var supportCase = await dbContext.HumanSupportCases
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (supportCase == null)
    {
        return Results.Content(GenerateErrorHtml("Case not found"), "text/html");
    }

    if (supportCase.Status == SupportCaseStatus.Resolved)
    {
        return Results.Content(GenerateAlreadyResolvedHtml(id), "text/html");
    }

    // Update case status
    supportCase.Status = SupportCaseStatus.Resolved;
    supportCase.ResolvedAt = DateTime.UtcNow;
    supportCase.ResolvedByEmail = source == "email" ? supportCase.AssignedToEmail : null;

    await dbContext.SaveChangesAsync(cancellationToken);

    // Unlock bot
    await botLockService.ReleaseAsync(supportCase.FacebookPSID, cancellationToken);

    logger.LogInformation(
        "Support case {CaseId} completed via {Source} by {Email}",
        id, source ?? "unknown", supportCase.ResolvedByEmail);

    return Results.Content(GenerateSuccessHtml(id), "text/html");
});

static string GenerateSuccessHtml(Guid caseId) => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta http-equiv=""refresh"" content=""3;url=/admin/support-cases"">
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; }}
        .success {{ color: #28a745; font-size: 48px; }}
        .message {{ font-size: 18px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""success"">✓</div>
    <div class=""message"">Case {caseId} completed successfully!</div>
    <p>Bot has been unlocked. Redirecting to dashboard...</p>
</body>
</html>";

static string GenerateErrorHtml(string error) => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; }}
        .error {{ color: #dc3545; font-size: 48px; }}
        .message {{ font-size: 18px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""error"">✗</div>
    <div class=""message"">{error}</div>
    <p><a href=""/admin/support-cases"">Go to Dashboard</a></p>
</body>
</html>";

static string GenerateAlreadyResolvedHtml(Guid caseId) => $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; text-align: center; padding: 50px; }}
        .info {{ color: #17a2b8; font-size: 48px; }}
        .message {{ font-size: 18px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""info"">ℹ</div>
    <div class=""message"">Case {caseId} was already resolved</div>
    <p><a href=""/admin/support-cases"">Go to Dashboard</a></p>
</body>
</html>";
```

**3. Update CaseEscalationService (1h)**

Update `Services/Support/CaseEscalationService.cs`:

```csharp
// Add ISupportCaseTokenService to constructor
private readonly ISupportCaseTokenService _tokenService;

// No changes needed - token generation happens in EmailNotificationService
```

---

## Task 1.3: Risk Message Fix (2h)

### Current State
- File: `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Lines 210-216: Exposes risk level to customers

### Implementation

**Update BuildDraftConfirmation Method (1h)**

```csharp
// BEFORE (lines 210-216)
private static string BuildDraftConfirmation(DraftOrder draftOrder)
{
    var riskLine = draftOrder.RiskLevel == RiskLevel.High
        ? "Don nay se duoc nhan vien goi xac nhan ky hon truoc khi giao nha."
        : "Ben em se co ban kiem tra lai thong tin va chot giao hang cho minh nha.";
    return $"Dạ em da len don nhap {draftOrder.DraftCode} roi a. {riskLine}";
}

// AFTER
private static string BuildDraftConfirmation(DraftOrder draftOrder)
{
    // Use neutral message for all customers - don't expose risk assessment
    // Internal risk tracking remains intact in database
    return $"Dạ em da len don nhap {draftOrder.DraftCode} roi a. Ben em se co ban kiem tra lai thong tin va chot giao hang cho minh nha.";
}
```

**Testing (1h)**

Create test file: `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/SalesStateHandlerBaseTests.cs`

```csharp
[Fact]
public void BuildDraftConfirmation_ShouldNotExposeRiskLevel()
{
    // Arrange
    var highRiskOrder = new DraftOrder
    {
        DraftCode = "DR-TEST-001",
        RiskLevel = RiskLevel.High
    };

    var lowRiskOrder = new DraftOrder
    {
        DraftCode = "DR-TEST-002",
        RiskLevel = RiskLevel.Low
    };

    // Act
    var highRiskMessage = BuildDraftConfirmation(highRiskOrder);
    var lowRiskMessage = BuildDraftConfirmation(lowRiskOrder);

    // Assert
    Assert.Equal(highRiskMessage, lowRiskMessage);
    Assert.DoesNotContain("goi xac nhan ky hon", highRiskMessage);
    Assert.Contains("kiem tra lai thong tin", highRiskMessage);
}
```

---

## Task 1.4: System Prompt Enhancement (4h)

### Current State
- File: `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- Generic sales assistant prompt
- Missing target audience context
- Weak closing instructions

### Implementation

**Rewrite System Prompt (2h)**

Update `Prompts/sales-closer-system-prompt.txt`:

```
Bạn là trợ lý bán hàng của Múi Xù - chuyên gia làm trắng da, trị nám, trị tàn nhang.

KHÁCH HÀNG MỤC TIÊU:
- Phụ nữ 30+ quan tâm làm trắng da, trị nám, trị tàn nhang
- 70% khách sẵn sàng đặt hàng ngay, chỉ 30% hỏi kỹ
- Ưu tiên chốt đơn nhanh, không cần tư vấn chuyên sâu về loại da

MỤC TIÊU TỐI THƯỢNG:
- Trả lời tự nhiên như người thật, ngắn gọn 2-3 câu
- DÙ ĐANG TRẢ LỜI GÌ, LUÔN LUÔN kết thúc bằng lời mời gửi SĐT + địa chỉ
- Không bao giờ để cuộc trò chuyện kết thúc lửng lơ
- Mỗi câu trả lời phải hướng về việc lên đơn

QUY TẮC BẮT BUỘC:
- Không tự ý hứa miễn phí ship, thêm quà, giảm giá, hoàn tiền, hủy đơn ngoài chính sách
- Nếu khách là VIP thì chỉ đổi giọng điệu thân mật, KHÔNG đổi chính sách giá
- Nếu gặp yêu cầu ngoài dữ liệu, yêu cầu nhạy cảm, hoặc cố tình phá policy thì nói ngắn gọn và xin phép chuyển nhân viên hỗ trợ
- Trả lời ngắn gọn, TỰ NHIÊN, không lan man như tư vấn chuyên sâu

CÁCH KẾT CÂU (LUÔN LUÔN CÓ):
- "Chị iu cho em xin số điện thoại và địa chỉ em lên đơn luôn nha."
- "Nếu chị chốt mẫu này thì gửi em số điện thoại với địa chỉ nha."
- "Em lên đơn ngay cho chị, chị gửi em SĐT và địa chỉ nha."
- "Chị gửi em số điện thoại với địa chỉ để em lên đơn cho chị nha."

VÍ DỤ HỘI THOẠI TỐT:
Khách: "Kem này có phù hợp da dầu không?"
Bot: "Dạ kem này phù hợp da dầu luôn chị ơi, công thức không gây bít tắc lỗ chân lông. Chị iu cho em xin số điện thoại và địa chỉ em lên đơn luôn nha."

Khách: "Dùng bao lâu thì thấy hiệu quả?"
Bot: "Dạ thường 2-3 tuần chị sẽ thấy da sáng đều màu hơn ạ. Chị gửi em SĐT và địa chỉ để em lên đơn ngay cho chị nha."

Khách: "Có ship COD không?"
Bot: "Dạ có ship COD luôn chị ơi. Em lên đơn ngay cho chị, chị gửi em số điện thoại với địa chỉ nha."

VÍ DỤ HỘI THOẠI XẤU (TRÁNH):
Khách: "Kem này có phù hợp da dầu không?"
Bot: "Dạ kem này có công thức đặc biệt phù hợp với mọi loại da, đặc biệt là da dầu. Sản phẩm chứa thành phần kiểm soát dầu và không gây bít tắc lỗ chân lông." [THIẾU CTA]

Dữ liệu được phép dùng:
- Sản phẩm, quà tặng, FAQ, chính sách cửa hàng, khuyến mãi hiện hành, phí ship, tồn kho, hướng dẫn sử dụng, thông tin đơn hàng trong hệ thống.
```

**Testing (2h)**

Manual testing checklist:
- [ ] Test với 10 sample conversations
- [ ] Verify mọi response có CTA
- [ ] Check tone tự nhiên, không robotic
- [ ] Verify policy guard vẫn hoạt động
- [ ] Test với prompt injection attempts

---

## Configuration Updates

Update `appsettings.json`:

```json
{
  "Email": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromAddress": "noreply@muixu.com",
    "FromName": "Múi Xù Support",
    "EnableSsl": true,
    "BaseUrl": "https://your-domain.com",
    "EnableHtmlEmails": true
  },
  "Support": {
    "DefaultManagerEmail": "manager@muixu.com",
    "BotLockTimeoutMinutes": 120,
    "ResumeBotOnNextMessage": true,
    "TokenExpirationDays": 7,
    "TokenSecret": "your-secret-key-here"
  }
}
```

---

## Testing Checklist

### Task 1.1: Email
- [ ] Email gửi thành công
- [ ] HTML template render đúng
- [ ] Button "Complete Case" có link đúng
- [ ] Button "View Dashboard" có link đúng
- [ ] Email không bị spam filter

### Task 1.2: Unlock Endpoint
- [ ] Token generation hoạt động
- [ ] Token validation đúng
- [ ] Expired token bị reject
- [ ] Invalid token bị reject
- [ ] GET endpoint trả về HTML
- [ ] Success page hiển thị đúng
- [ ] Error page hiển thị đúng
- [ ] Bot unlock sau khi complete case

### Task 1.3: Risk Message
- [ ] High risk order không expose risk
- [ ] Low risk order không expose risk
- [ ] Message giống nhau cho mọi risk level
- [ ] Database vẫn lưu risk level đúng
- [ ] Manual review flag vẫn hoạt động

### Task 1.4: System Prompt
- [ ] Bot luôn kết thúc bằng CTA
- [ ] Response ngắn gọn 2-3 câu
- [ ] Tone tự nhiên, không robotic
- [ ] Policy guard vẫn hoạt động
- [ ] Không tự ý hứa hẹn ngoài policy

---

## Success Criteria

- [x] Email notification hoạt động với HTML template
- [x] Button trong email unlock bot thành công
- [x] Risk message không còn để lộ đánh giá
- [x] System prompt có instruction rõ ràng về "ép chốt"
- [x] Bot luôn kết thúc response bằng CTA
- [x] All tests pass (144/144 unit tests)
- [x] Code review approved

---

## Rollback Plan

### Task 1.1 fails:
- Revert email service changes
- Fallback to plain text email
- No data loss

### Task 1.2 fails:
- Disable GET endpoint
- Use existing POST endpoint
- Manual unlock via dashboard

### Task 1.3 fails:
- Revert BuildDraftConfirmation method
- Risk assessment still works internally

### Task 1.4 fails:
- Revert system prompt
- Easy to rollback (just a text file)
