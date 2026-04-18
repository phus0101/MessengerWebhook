using MessengerWebhook.Services.ResponseValidation.Configuration;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.ResponseValidation.Validators;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Services.ResponseValidation;

/// <summary>
/// Main service for validating bot responses
/// </summary>
public class ResponseValidationService : IResponseValidationService
{
    private readonly ToneConsistencyValidator _toneValidator;
    private readonly ContextAppropriatenessValidator _contextValidator;
    private readonly VietnameseQualityValidator _languageValidator;
    private readonly StructureValidator _structureValidator;
    private readonly ILogger<ResponseValidationService> _logger;
    private readonly ResponseValidationOptions _options;

    public ResponseValidationService(
        IOptions<ResponseValidationOptions> options,
        ILogger<ResponseValidationService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _toneValidator = new ToneConsistencyValidator();
        _contextValidator = new ContextAppropriatenessValidator();
        _languageValidator = new VietnameseQualityValidator();
        _structureValidator = new StructureValidator();
    }

    public async Task<ValidationResult> ValidateAsync(
        ResponseValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableValidation)
        {
            return new ValidationResult { IsValid = true };
        }

        var startTime = DateTime.UtcNow;
        var allIssues = new List<ValidationIssue>();

        try
        {
            // Run validators
            if (_options.EnableToneValidation)
            {
                allIssues.AddRange(_toneValidator.Validate(context.Response, context.ToneProfile));
            }

            if (_options.EnableContextValidation)
            {
                allIssues.AddRange(_contextValidator.Validate(context.Response, context.ConversationContext));
            }

            if (_options.EnableLanguageValidation)
            {
                allIssues.AddRange(_languageValidator.Validate(context.Response));
            }

            if (_options.EnableStructureValidation)
            {
                allIssues.AddRange(_structureValidator.Validate(context.Response, _options));
            }

            // Categorize by severity
            var errors = allIssues.Where(i => i.Severity >= ValidationSeverity.Error).ToList();
            var warnings = allIssues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();
            var infos = allIssues.Where(i => i.Severity == ValidationSeverity.Info).ToList();

            var isValid = !errors.Any() || !_options.BlockOnErrors;

            var duration = DateTime.UtcNow - startTime;

            if (!isValid)
            {
                _logger.LogWarning(
                    "Response validation failed with {ErrorCount} errors in {Duration}ms: {Errors}",
                    errors.Count,
                    duration.TotalMilliseconds,
                    string.Join("; ", errors.Select(e => $"{e.Category}: {e.Message}")));
            }
            else if (warnings.Any())
            {
                _logger.LogInformation(
                    "Response validation passed with {WarningCount} warnings in {Duration}ms",
                    warnings.Count,
                    duration.TotalMilliseconds);
            }

            return await Task.FromResult(new ValidationResult
            {
                IsValid = isValid,
                Issues = errors,
                Warnings = warnings,
                Metadata = new Dictionary<string, object>
                {
                    ["ValidationDurationMs"] = duration.TotalMilliseconds,
                    ["InfoCount"] = infos.Count,
                    ["TotalIssuesFound"] = allIssues.Count
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during response validation");

            // Fail-safe: if validation crashes, block response in production
            if (_options.BlockOnValidationError)
            {
                return await Task.FromResult(new ValidationResult
                {
                    IsValid = false,
                    Issues = new List<ValidationIssue>
                    {
                        new()
                        {
                            Severity = ValidationSeverity.Error,
                            Category = "System",
                            Message = $"Validation system error: {ex.Message}"
                        }
                    }
                }).ConfigureAwait(false);
            }

            // In development, allow through with warning
            return await Task.FromResult(new ValidationResult
            {
                IsValid = true,
                Warnings = new List<ValidationIssue>
                {
                    new()
                    {
                        Severity = ValidationSeverity.Warning,
                        Category = "System",
                        Message = $"Validation error: {ex.Message}"
                    }
                }
            }).ConfigureAwait(false);
        }
    }
}
