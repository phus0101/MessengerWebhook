# Phase 0: Enable Config Validation (C1)

## Overview
- Priority: Critical
- Current status: Not started
- Effort: 30min
- Issue: C1 — Config validation commented out in Program.cs lines 397-402

## Problem
`FacebookOptions` validation (AppSecret, PageAccessToken) and `WebhookOptions` validation (VerifyToken) are commented out, allowing app to start without credentials and fail silently at runtime.

## Context Links
- `src/MessengerWebhook/Program.cs:397-402` (commented code)
- `src/MessengerWebhook/Configuration/FacebookOptions.cs` (options class)
- `src/MessengerWebhook/Authentication/FacebookAuthOptionsValidator.cs` (already exists, needs check)
- `src/MessengerWebhook/Configuration/WebhookOptions.cs`

## Architecture

Use `IValidateOptions<T>` pattern — the .NET 8 idiomatic approach:

```
Program.cs startup
  └── IOptions<FacebookOptions>
        └── IValidateOptions<FacebookOptions>.Validate()  ← throws InvalidOperationException
              └── Checks: AppSecret, PageAccessToken (fallback-aware)
  └── IOptions<WebhookOptions>  
        └── IValidateOptions<WebhookOptions>.Validate()  ← throws InvalidOperationException
              └── Checks: VerifyToken
```

Remove commented code. Remove the manual `validationDbContext` query that checks for page access token overrides — let the validator handle it.

## Implementation Steps

### Step 1: Create FacebookOptionsValidator.cs

Create `src/MessengerWebhook/Configuration/Validators/FacebookOptionsValidator.cs`:

```csharp
public class FacebookOptionsValidator : IValidateOptions<FacebookOptions>
{
    private readonly MessengerBotDbContext _dbContext;
    private readonly bool _skipValidationInDevelopment;

    public ValidateOptionsResult Validate(string? name, FacebookOptions options)
    {
        if (_skipValidationInDevelopment && IsDevelopment())
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.AppSecret))
            return ValidateOptionsResult.Fail("Facebook:AppSecret is required.");

        var hasPageToken = !string.IsNullOrWhiteSpace(options.PageAccessToken);
        var hasPageOverride = _dbContext.FacebookPageConfigs
            .IgnoreQueryFilters()
            .Any(x => x.IsActive && !string.IsNullOrWhiteSpace(x.PageAccessToken));

        if (!hasPageToken && !hasPageOverride)
            return ValidateOptionsResult.Fail("Facebook:PageAccessToken is required and no page override exists.");

        return ValidateOptionsResult.Success;
    }
}
```

### Step 2: Create WebhookOptionsValidator.cs

Create `src/MessengerWebhook/Configuration/Validators/WebhookOptionsValidator.cs`:

```csharp
public class WebhookOptionsValidator : IValidateOptions<WebhookOptions>
{
    public ValidateOptionsResult Validate(string? name, WebhookOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.VerifyToken))
            return ValidateOptionsResult.Fail("Webhook:VerifyToken is required.");
        return ValidateOptionsResult.Success;
    }
}
```

### Step 3: Register validators in Program.cs

Before `builder.Build()`, add:
```csharp
builder.Services.AddSingleton<IValidateOptions<FacebookOptions>>(sp =>
    new FacebookOptionsValidator(sp.GetRequiredService<MessengerBotDbContext>()));
builder.Services.AddSingleton<IValidateOptions<WebhookOptions>, WebhookOptionsValidator>();
```

### Step 4: Remove commented validation block (lines 397-402)

Delete lines 391-402 in Program.cs (the entire validation block including the `validationScope` query). The `IValidateOptions` validators will run automatically during `builder.Build()`.

## Related Code Files

**To create:**
- `src/MessengerWebhook/Configuration/Validators/FacebookOptionsValidator.cs`
- `src/MessengerWebhook/Configuration/Validators/WebhookOptionsValidator.cs`

**To modify:**
- `src/MessengerWebhook/Program.cs` (register validators, remove commented block)

## Todo List

- [ ] Create FacebookOptionsValidator.cs
- [ ] Create WebhookOptionsValidator.cs
- [ ] Register validators in Program.cs
- [ ] Remove commented validation block and redundant validationScope
- [ ] Verify app still builds

## Success Criteria

- `dotnet build` succeeds
- App throws on startup if Facebook:AppSecret is missing
- App throws on startup if Webhook:VerifyToken is missing
- App starts normally when credentials are present

## Risk Assessment

**Low risk.** Pure addition of validation, removal of dead (commented) code. Main risk is breaking local dev if .env is missing — mitigated by `_skipValidationInDevelopment` flag.
