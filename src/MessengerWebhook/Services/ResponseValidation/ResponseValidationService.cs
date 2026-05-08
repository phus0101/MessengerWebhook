using System.Globalization;
using System.Text.RegularExpressions;
using MessengerWebhook.Services.ProductGrounding;
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
    private static readonly Regex PriceRegex = new(@"(?<amount>\d{1,3}(?:[\.,]\d{3})+|\d+)(?:\s*)(?:đ|₫|vnd|vnđ|k|nghìn|ngàn)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly ProductMentionDetector ProductMentionDetector = new();

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

            allIssues.AddRange(ValidateGroundedFacts(context));

            // Categorize by severity
            var errors = allIssues.Where(i => i.Severity >= ValidationSeverity.Error).ToList();
            var warnings = allIssues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();
            var infos = allIssues.Where(i => i.Severity == ValidationSeverity.Info).ToList();

            var groundingErrors = errors.Where(i => i.Category == "Grounding").ToList();
            var isValid = !groundingErrors.Any() && (!errors.Any() || !_options.BlockOnErrors);

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

    private static List<ValidationIssue> ValidateGroundedFacts(ResponseValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Response))
        {
            return new List<ValidationIssue>();
        }

        var productMentions = ProductMentionDetector.ExtractProductMentions(context.Response);
        context.ResponseContainsProductMention = productMentions.Count > 0;
        if (!context.RequiresFactGrounding && productMentions.Count == 0)
        {
            return new List<ValidationIssue>();
        }

        var issues = new List<ValidationIssue>();
        issues.AddRange(ValidateProductNames(context));
        issues.AddRange(ValidatePrices(context));

        if (!context.AllowPolicyFacts && ContainsAny(context.Response, "freeship", "miễn phí ship", "mien phi ship", "giảm giá", "giam gia", "quà tặng", "qua tang", "đổi trả", "doi tra", "hoàn tiền", "hoan tien", "bảo hành", "bao hanh"))
        {
            issues.Add(CreateGroundingIssue("Ungrounded policy fact detected"));
        }

        if (!context.AllowInventoryFacts && ContainsAny(context.Response, "còn hàng", "con hang", "hết hàng", "het hang", "tồn kho", "ton kho", "còn "))
        {
            issues.Add(CreateGroundingIssue("Ungrounded inventory fact detected"));
        }

        if (!context.AllowOrderFacts && ContainsAny(context.Response, "đã nhận thanh toán", "da nhan thanh toan", "đang chuẩn bị hàng", "dang chuan bi hang", "đã xác nhận", "da xac nhan", "dự kiến giao", "du kien giao"))
        {
            issues.Add(CreateGroundingIssue("Ungrounded order fact detected"));
        }

        return issues;
    }

    private static IEnumerable<ValidationIssue> ValidateProductNames(ResponseValidationContext context)
    {
        foreach (var candidate in ProductMentionDetector.ExtractProductMentions(context.Response))
        {
            if (context.AllowedProductNames.Any(productName => ContainsEquivalent(candidate, productName)) ||
                context.AllowedProductCodes.Any(code => ContainsProductCode(candidate, code)))
            {
                continue;
            }

            yield return CreateGroundingIssue($"Ungrounded product fact detected: {candidate}");
        }
    }

    private static IEnumerable<ValidationIssue> ValidatePrices(ResponseValidationContext context)
    {
        foreach (Match match in PriceRegex.Matches(context.Response))
        {
            var parsedPrice = ParsePrice(match.Groups["amount"].Value, match.Value);
            if (parsedPrice.HasValue && context.AllowedPrices.Any(price => price == parsedPrice.Value))
            {
                continue;
            }

            yield return CreateGroundingIssue($"Ungrounded price fact detected: {match.Value}");
        }
    }

    private static decimal? ParsePrice(string amountText, string fullMatch)
    {
        var normalized = amountText.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        return fullMatch.Contains('k', StringComparison.OrdinalIgnoreCase) ? amount * 1000m : amount;
    }

    private static bool ContainsEquivalent(string responseProduct, string allowedProduct)
    {
        var normalizedResponse = NormalizeProductName(responseProduct);
        var normalizedAllowed = NormalizeProductName(allowedProduct);

        if (string.Equals(normalizedResponse, normalizedAllowed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalizedResponse.StartsWith(normalizedAllowed + " cho ", StringComparison.OrdinalIgnoreCase) ||
               normalizedResponse.StartsWith(normalizedAllowed + " san pham nay ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsProductCode(string responseProduct, string allowedCode)
    {
        if (string.IsNullOrWhiteSpace(allowedCode))
        {
            return false;
        }

        return Regex.IsMatch(responseProduct, $@"(?<![\p{{L}}\p{{M}}0-9]){Regex.Escape(allowedCode)}(?![\p{{L}}\p{{M}}0-9])", RegexOptions.IgnoreCase);
    }

    private static string NormalizeProductName(string value) =>
        MessengerWebhook.Services.ProductGrounding.ProductNameNormalizer.Normalize(value);

    private static bool ContainsAny(string value, params string[] phrases)
    {
        var normalizedValue = NormalizeProductName(value);
        return phrases.Any(phrase => normalizedValue.Contains(NormalizeProductName(phrase), StringComparison.OrdinalIgnoreCase));
    }

    private static ValidationIssue CreateGroundingIssue(string message)
    {
        return new ValidationIssue
        {
            Severity = ValidationSeverity.Error,
            Category = "Grounding",
            Message = message,
            SuggestedFix = "Use only facts grounded in active tenant catalog or runtime state"
        };
    }
}
