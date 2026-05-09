using MessengerWebhook.Configuration;
using MessengerWebhook.Models;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.AI;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Models;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.RAG;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.StateMachine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;

namespace MessengerWebhook.Services.Sales.Reply;

/// <summary>
/// Orchestrates the AI reply pipeline. Extracted from SalesStateHandlerBase (R-04) verbatim:
/// behavior, branching, mutation order, and StateContext keys are preserved exactly.
/// </summary>
public sealed class SalesReplyOrchestrator : ISalesReplyOrchestrator
{
    private readonly IGeminiService _geminiService;
    private readonly IRAGService? _ragService;
    private readonly IEmotionDetectionService _emotionService;
    private readonly IToneMatchingService _toneService;
    private readonly IConversationContextAnalyzer _conversationAnalyzer;
    private readonly ISmallTalkService _smallTalkService;
    private readonly IResponseValidationService _validationService;
    private readonly IABTestService _abTestService;
    private readonly IConversationMetricsService _metricsService;
    private readonly ICustomerIntelligenceService _customerService;
    private readonly IProductGroundingService _productGrounding;
    private readonly ISalesContextResolver _contextResolver;
    private readonly ISalesPromptBuilder _promptBuilder;
    private readonly SalesBotOptions _salesBotOptions;
    private readonly RAGOptions _ragOptions;
    private readonly ILogger _logger;

    public SalesReplyOrchestrator(
        IGeminiService geminiService,
        IRAGService? ragService,
        IEmotionDetectionService emotionService,
        IToneMatchingService toneService,
        IConversationContextAnalyzer conversationAnalyzer,
        ISmallTalkService smallTalkService,
        IResponseValidationService validationService,
        IABTestService abTestService,
        IConversationMetricsService metricsService,
        ICustomerIntelligenceService customerService,
        IProductGroundingService productGrounding,
        ISalesContextResolver contextResolver,
        ISalesPromptBuilder promptBuilder,
        IOptions<SalesBotOptions> salesBotOptions,
        IOptions<RAGOptions> ragOptions,
        ILogger logger)
    {
        _geminiService = geminiService;
        _ragService = ragService;
        _emotionService = emotionService;
        _toneService = toneService;
        _conversationAnalyzer = conversationAnalyzer;
        _smallTalkService = smallTalkService;
        _validationService = validationService;
        _abTestService = abTestService;
        _metricsService = metricsService;
        _customerService = customerService;
        _productGrounding = productGrounding;
        _contextResolver = contextResolver;
        _promptBuilder = promptBuilder;
        _salesBotOptions = salesBotOptions.Value;
        _ragOptions = ragOptions.Value;
        _logger = logger;
    }

    public Task<string> GenerateAsync(SalesReplyRequest request)
        => BuildNaturalReplyAsync(request.Context, request.Message, request.Intent, request.SubIntent);

    public Task<string> BuildGroundedFallbackAsync(StateContext ctx, string message, GroundedProductContext groundingContext)
        => BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null);

    // ── Pipeline (moved verbatim from SalesStateHandlerBase) ──────────────────

    private async Task<string> BuildNaturalReplyAsync(StateContext ctx, string message, Services.AI.Models.CustomerIntent? intent = null, SubIntentResult? subIntent = null)
    {
        var startTime = DateTime.UtcNow;

        // A/B Test: Check variant assignment
        var variant = await _abTestService.GetVariantAsync(ctx.FacebookPSID, ctx.SessionId, CancellationToken.None);
        ctx.SetData("abTestVariant", variant);

        _logger.LogInformation(
            "A/B Test variant for PSID {PSID}: {Variant} (Enabled: {Enabled})",
            ctx.FacebookPSID,
            variant,
            _abTestService.IsEnabled());

        // Control group: Skip naturalness pipeline, use direct AI response
        if (variant == "control")
        {
            _logger.LogInformation("Control group: Skipping naturalness pipeline for PSID {PSID}", ctx.FacebookPSID);
            var controlResponse = await GenerateDirectAIResponseAsync(ctx, message, intent);

            // Log control metrics (no pipeline data)
            await LogMetricsAsync(ctx, startTime, null, null, null, null, null, null);

            return controlResponse;
        }

        // Treatment group: Run full naturalness pipeline
        _logger.LogInformation("Treatment group: Running full naturalness pipeline for PSID {PSID}", ctx.FacebookPSID);

        var pipelineStartTime = DateTime.UtcNow;

        var history = GetHistory(ctx);
        var activeProducts = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
        var productCodes = activeProducts.Select(product => product.Code).ToList();
        var contactSummary = _promptBuilder.GetContactSummary(ctx);

        // Get SubIntent from context if available
        var contextSubIntent = ctx.GetData<SubIntentResult?>("subIntent");
        var includeDetailedInfo = contextSubIntent?.Category == SubIntentCategory.ProductQuestion;

        var earlyRagContext = await RetrieveRagContextAsync(ctx, message, includeDetailedInfo);
        var groundingContext = await _productGrounding.BuildContextWithRelatedSuggestionsAsync(message, activeProducts, earlyRagContext.Products);
        if (groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts)
        {
            return await BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null);
        }

        history = _productGrounding.SanitizeAssistantHistory(history, groundingContext.AllowedProducts).ToList();

        // Get VIP profile BEFORE building prompt
        var vipProfile = await _contextResolver.GetVipProfileAsync(ctx);
        var hasAssistantReply = history.Any(m => m.Role == "assistant");
        var hasGreeted = ctx.GetData<bool?>("vipGreetingSent") == true;

        // Get returning customer flag from context
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;

        var shouldGreet = !hasAssistantReply && !hasGreeted;
        var vipInstruction = _promptBuilder.BuildCustomerInstruction(vipProfile, shouldGreet, isReturningCustomer);

        if (shouldGreet)
        {
            ctx.SetData("vipGreetingSent", true);
            _logger.LogInformation("First greeting sent for PSID: {PSID}", ctx.FacebookPSID);
        }

        // Build CTA context with intent awareness
        var ctaContext = _promptBuilder.BuildCtaContext(ctx, intent);

        // Detect emotion and generate tone profile
        var emotion = await _emotionService.DetectEmotionWithContextAsync(
            message,
            history.Select(h => new Services.AI.Models.ConversationMessage
            {
                Role = h.Role,
                Content = h.Content
            }).ToList(),
            CancellationToken.None);

        // Analyze conversation context
        var conversationContext = await _conversationAnalyzer.AnalyzeWithEmotionAsync(
            history.Select(h => new Services.AI.Models.ConversationMessage
            {
                Role = h.Role,
                Content = h.Content
            }).ToList(),
            new List<Services.Emotion.Models.EmotionScore> { emotion },
            CancellationToken.None);

        // Store context for decision-making
        ctx.SetData("conversationContext", conversationContext);

        _logger.LogInformation(
            "Conversation analysis - PSID: {PSID}, Stage: {Stage}, Quality: {Quality:F1}, Patterns: {PatternCount}, Insights: {InsightCount}",
            ctx.FacebookPSID,
            conversationContext.CurrentStage,
            conversationContext.Quality.Score,
            conversationContext.Patterns.Count,
            conversationContext.Insights.Count);

        var customer = await _customerService.GetExistingAsync(
            ctx.FacebookPSID,
            ctx.GetData<string>("facebookPageId"));

        var toneProfile = customer != null && vipProfile != null
            ? await _toneService.GenerateToneProfileAsync(
                emotion,
                vipProfile,
                customer,
                conversationTurnCount: history.Count,
                CancellationToken.None)
            : null;

        // Build tone instructions for prompt
        var toneInstruction = toneProfile != null
            ? $"""
## Tone Adaptation
Xung ho: {toneProfile.PronounText}
{string.Join("\n", toneProfile.ToneInstructions.Select(kv => $"- {kv.Value}"))}
"""
            : string.Empty;

        // Store tone profile in context for logging
        if (toneProfile != null)
        {
            ctx.SetData("toneProfile", toneProfile);
            ctx.SetData("emotionScore", emotion);

            _logger.LogInformation(
                "Tone profile generated for PSID: {PSID} - Emotion: {Emotion}, Tone: {Tone}, Pronoun: {Pronoun}, Escalation: {Escalation}",
                ctx.FacebookPSID,
                emotion.PrimaryEmotion,
                toneProfile.Level,
                toneProfile.PronounText,
                toneProfile.RequiresEscalation);
        }

        // Analyze for small talk
        var smallTalkResponse = toneProfile != null && vipProfile != null
            ? await _smallTalkService.AnalyzeAsync(
                message,
                emotion,
                toneProfile,
                conversationContext,
                vipProfile,
                isReturningCustomer,
                history.Count,
                CancellationToken.None)
            : null;

        // Store small talk response in context
        if (smallTalkResponse != null)
        {
            ctx.SetData("smallTalkResponse", smallTalkResponse);

            if (smallTalkResponse.IsSmallTalk)
            {
                _logger.LogInformation(
                    "Small talk detected for PSID: {PSID} - Intent: {Intent}, Confidence: {Confidence:F2}, Transition: {Transition}",
                    ctx.FacebookPSID,
                    smallTalkResponse.Intent,
                    smallTalkResponse.Confidence,
                    smallTalkResponse.TransitionReadiness);

                // For pure greetings with no business intent, return suggested response directly
                if (smallTalkResponse.TransitionReadiness == Services.SmallTalk.Models.TransitionReadiness.StayInSmallTalk &&
                    history.Count <= 2 &&
                    !string.IsNullOrWhiteSpace(smallTalkResponse.SuggestedResponse))
                {
                    var suggestedValidationContext = _promptBuilder.BuildFactValidationContext(
                        smallTalkResponse.SuggestedResponse,
                        toneProfile,
                        conversationContext,
                        smallTalkResponse,
                        message,
                        groundingContext.RequiresGrounding,
                        groundingContext.AllowedProducts,
                        allowPolicyFacts: false,
                        allowInventoryFacts: false,
                        allowOrderFacts: false);
                    var suggestedValidationResult = await _validationService.ValidateAsync(suggestedValidationContext, CancellationToken.None);
                    if (!suggestedValidationResult.IsValid)
                    {
                        _logger.LogWarning(
                            "Small talk suggested response validation failed for PSID {PSID}: {Issues}",
                            ctx.FacebookPSID,
                            string.Join("; ", suggestedValidationResult.Issues.Select(i => i.Message)));
                        return _promptBuilder.BuildProductGroundingFallbackReply();
                    }

                    AddToHistory(ctx, "assistant", smallTalkResponse.SuggestedResponse);
                    return smallTalkResponse.SuggestedResponse;
                }
            }
        }

        // Build SubIntent guidance text
        string? subIntentGuidance = null;
        if (subIntent != null)
        {
            subIntentGuidance = subIntent.Category switch
            {
                SubIntentCategory.ProductQuestion =>
                    "Khách hỏi chi tiết về sản phẩm. Hãy cung cấp thông tin đầy đủ về thành phần, công dụng, cách dùng.",
                SubIntentCategory.PriceQuestion =>
                    "Khách hỏi về giá. Hãy giải thích rõ giá, chương trình khuyến mãi, so sánh giá trị.",
                SubIntentCategory.ShippingQuestion =>
                    "Khách hỏi về vận chuyển. Hãy giải thích chính sách ship, thời gian giao hàng.",
                SubIntentCategory.AvailabilityQuestion =>
                    "Khách hỏi về tình trạng hàng. Hãy thông báo còn hàng hay hết, dự kiến nhập hàng.",
                SubIntentCategory.PolicyQuestion =>
                    "Khách hỏi về chính sách. Hãy giải thích chính sách đổi trả, bảo hành.",
                SubIntentCategory.ComparisonQuestion =>
                    "Khách muốn so sánh sản phẩm. Hãy so sánh ưu nhược điểm, phù hợp với nhu cầu nào.",
                _ => null
            };
        }

        var ragContext = string.IsNullOrWhiteSpace(earlyRagContext.FormattedContext)
            ? null
            : earlyRagContext.FormattedContext;

        var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
San pham duoc phep neu can neu ten: {_promptBuilder.FormatAllowedProductNames(groundingContext.AllowedProducts)}
Thong tin da co: {contactSummary}
{vipInstruction}

{toneInstruction}

Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Neu khach hoi FAQ/policy thi tra loi trong pham vi an toan.

{ctaContext}
""";

        var response = await _geminiService.SendMessageAsync(
            ctx.FacebookPSID,
            prompt,
            history,
            ragContext: ragContext,
            subIntentGuidance: subIntentGuidance);

        // Capture pipeline latency
        var pipelineLatency = (int)(DateTime.UtcNow - pipelineStartTime).TotalMilliseconds;

        // Validate response quality before sending to customer
        var validationContext = _promptBuilder.BuildFactValidationContext(
            response,
            toneProfile,
            conversationContext,
            smallTalkResponse,
            message,
            groundingContext.RequiresGrounding,
            groundingContext.AllowedProducts,
            allowPolicyFacts: false,
            allowInventoryFacts: false,
            allowOrderFacts: false);

        var validationResult = await _validationService.ValidateAsync(validationContext, CancellationToken.None);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Response validation failed for PSID {PSID}: {Issues}",
                ctx.FacebookPSID,
                string.Join("; ", validationResult.Issues.Select(i => i.Message)));
            return _promptBuilder.BuildProductGroundingFallbackReply();
        }

        if (validationResult.Warnings.Any())
        {
            _logger.LogInformation(
                "Response validation warnings for PSID {PSID}: {Count} warnings",
                ctx.FacebookPSID,
                validationResult.Warnings.Count);
        }

        // Log treatment metrics (with pipeline data)
        await LogMetricsAsync(
            ctx,
            startTime,
            pipelineLatency,
            emotion?.PrimaryEmotion.ToString(),
            emotion != null ? (decimal)emotion.Confidence : null,
            toneProfile?.PronounText,
            conversationContext?.CurrentStage.ToString(),
            validationResult
        );

        // Validation: Log if CTA missing but trust AI to follow instruction
        var hasCtaKeywords = new[] { "gui", "len don", "dia chi", "so dien thoai", "xac nhan", "chon san pham" }
            .Any(keyword => response.ToLower().Contains(keyword));

        if (!hasCtaKeywords)
        {
            _logger.LogWarning(
                "Response may be missing CTA for {PSID}. AI should follow CTA instruction in prompt.",
                ctx.FacebookPSID
            );
        }

        return response;
    }

    private async Task<string> BuildGroundedRelatedSuggestionOrFallbackAsync(
        StateContext ctx,
        string message,
        GroundedProductContext groundingContext,
        Services.Tone.Models.ToneProfile? toneProfile,
        Services.Conversation.Models.ConversationContext? conversationContext,
        Services.SmallTalk.Models.SmallTalkResponse? smallTalkResponse)
    {
        if (!groundingContext.HasRelatedSuggestions || string.IsNullOrWhiteSpace(groundingContext.RelatedSuggestionReply))
        {
            return groundingContext.FallbackReply;
        }

        var validationContext = _promptBuilder.BuildFactValidationContext(
            groundingContext.RelatedSuggestionReply,
            toneProfile,
            conversationContext,
            smallTalkResponse,
            message,
            requiresProductGrounding: true,
            groundingContext.RelatedSuggestions,
            allowPolicyFacts: false,
            allowInventoryFacts: false,
            allowOrderFacts: false);

        var validationResult = await _validationService.ValidateAsync(validationContext, CancellationToken.None);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Related product suggestion validation failed for PSID {PSID}: {Issues}",
                ctx.FacebookPSID,
                string.Join("; ", validationResult.Issues.Select(i => i.Message)));
            return groundingContext.FallbackReply;
        }

        return groundingContext.RelatedSuggestionReply;
    }

    private async Task<RAGContext> RetrieveRagContextAsync(StateContext ctx, string message, bool includeDetailedInfo = false)
    {
        if (!_ragOptions.Enabled || _ragService == null)
        {
            return new RAGContext(string.Empty, new List<string>(), new List<GroundedProduct>(), new RAGMetrics(TimeSpan.Zero, TimeSpan.Zero, 0, false, "disabled"));
        }

        try
        {
            var ragResult = await _ragService.RetrieveContextAsync(message, topK: _ragOptions.TopK, includeDetailedInfo);

            _logger.LogInformation(
                "RAG retrieved {Count} products in {Ms}ms for PSID: {PSID} (detailed: {Detailed})",
                ragResult.ProductIds.Count,
                ragResult.Metrics.TotalLatency.TotalMilliseconds,
                ctx.FacebookPSID,
                includeDetailedInfo);

            return ragResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG retrieval failed for PSID: {PSID}", ctx.FacebookPSID);
            return new RAGContext(string.Empty, new List<string>(), new List<GroundedProduct>(), new RAGMetrics(TimeSpan.Zero, TimeSpan.Zero, 0, false, "error"));
        }
    }

    /// <summary>
    /// Control group: Direct AI response without naturalness pipeline.
    /// </summary>
    private async Task<string> GenerateDirectAIResponseAsync(StateContext ctx, string message, Services.AI.Models.CustomerIntent? intent = null)
    {
        var history = GetHistory(ctx);
        var activeProducts = await _contextResolver.GetActiveSelectedProductsAsync(ctx);
        var productCodes = activeProducts.Select(product => product.Code).ToList();
        var contactSummary = _promptBuilder.GetContactSummary(ctx);

        // Get VIP profile for customer instruction only
        var vipProfile = await _contextResolver.GetVipProfileAsync(ctx);
        var hasAssistantReply = history.Any(m => m.Role == "assistant");
        var hasGreeted = ctx.GetData<bool?>("vipGreetingSent") == true;
        var isReturningCustomer = ctx.GetData<bool?>("isReturningCustomer") == true;
        var shouldGreet = !hasAssistantReply && !hasGreeted;
        var vipInstruction = _promptBuilder.BuildCustomerInstruction(vipProfile, shouldGreet, isReturningCustomer);

        if (shouldGreet)
        {
            ctx.SetData("vipGreetingSent", true);
        }

        // Build CTA context
        var ctaContext = _promptBuilder.BuildCtaContext(ctx, intent);

        // RAG context retrieval if enabled
        string? ragContext = null;
        var ragProducts = new List<GroundedProduct>();
        if (_ragOptions.Enabled && _ragService != null)
        {
            try
            {
                var ragResult = await _ragService.RetrieveContextAsync(
                    message,
                    topK: _ragOptions.TopK);

                ragContext = ragResult.FormattedContext;
                ragProducts = ragResult.Products;

                _logger.LogInformation(
                    "RAG retrieved {Count} products in {Ms}ms for PSID: {PSID} (control group)",
                    ragResult.ProductIds.Count,
                    ragResult.Metrics.TotalLatency.TotalMilliseconds,
                    ctx.FacebookPSID);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG retrieval failed for control group PSID: {PSID}", ctx.FacebookPSID);
            }
        }

        var groundingContext = await _productGrounding.BuildContextWithRelatedSuggestionsAsync(message, activeProducts, ragProducts);
        if (groundingContext.RequiresGrounding && !groundingContext.HasAllowedProducts)
        {
            return await BuildGroundedRelatedSuggestionOrFallbackAsync(ctx, message, groundingContext, null, null, null);
        }

        history = _productGrounding.SanitizeAssistantHistory(history, groundingContext.AllowedProducts).ToList();

        // Simple prompt without tone/emotion/context instructions
        var prompt = $"""
Khach vua nhan: "{message}"
San pham dang quan tam: {(productCodes.Count == 0 ? "chua xac dinh" : string.Join(", ", productCodes))}
San pham duoc phep neu can neu ten: {_promptBuilder.FormatAllowedProductNames(groundingContext.AllowedProducts)}
Thong tin da co: {contactSummary}
{vipInstruction}

Quy tac:
- Tra loi tu nhien, ngan gon, giong nhan vien page.
- Khong tu y them qua, freeship, giam gia, huy don, hoan tien.
- Neu khach hoi FAQ/policy thi tra loi trong pham vi an toan.

{ctaContext}
""";

        var response = await _geminiService.SendMessageAsync(
            ctx.FacebookPSID,
            prompt,
            history,
            ragContext: ragContext);

        var validationContext = _promptBuilder.BuildFactValidationContext(
            response,
            null,
            null,
            null,
            message,
            groundingContext.RequiresGrounding,
            groundingContext.AllowedProducts,
            allowPolicyFacts: false,
            allowInventoryFacts: false,
            allowOrderFacts: false);
        var validationResult = await _validationService.ValidateAsync(validationContext, CancellationToken.None);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Direct AI response validation failed for PSID {PSID}: {Issues}",
                ctx.FacebookPSID,
                string.Join("; ", validationResult.Issues.Select(i => i.Message)));
            return _promptBuilder.BuildProductGroundingFallbackReply();
        }

        return response;
    }

    private async Task LogMetricsAsync(
        StateContext ctx,
        DateTime startTime,
        int? pipelineLatencyMs,
        string? detectedEmotion,
        decimal? emotionConfidence,
        string? matchedTone,
        string? journeyStage,
        ValidationResult? validationResult)
    {
        try
        {
            var totalResponseTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var history = GetHistory(ctx);
            var variant = ctx.GetData<string>("abTestVariant") ?? "control";

            var metricData = new ConversationMetricData
            {
                SessionId = ctx.SessionId,
                FacebookPSID = ctx.FacebookPSID,
                ABTestVariant = variant,
                MessageTimestamp = DateTime.UtcNow,
                ConversationTurn = history.Count,
                TotalResponseTimeMs = totalResponseTime,
                PipelineLatencyMs = pipelineLatencyMs,
                DetectedEmotion = detectedEmotion,
                EmotionConfidence = emotionConfidence,
                MatchedTone = matchedTone,
                JourneyStage = journeyStage,
                ValidationPassed = validationResult?.IsValid,
                ValidationErrors = validationResult?.Issues?.Any() == true
                    ? validationResult.Issues.ToDictionary(e => e.Category, e => (object)e.Message)
                    : null,
                ConversationOutcome = null
            };

            await _metricsService.LogAsync(metricData);

            _logger.LogDebug(
                "Metrics logged - PSID: {PSID}, Variant: {Variant}, Latency: {Latency}ms",
                ctx.FacebookPSID,
                variant,
                totalResponseTime);
        }
        catch (Exception ex)
        {
            // Never fail user request due to metrics logging
            _logger.LogError(ex, "Failed to log metrics for PSID: {PSID}", ctx.FacebookPSID);
        }
    }

    // ── History helpers (mirror SalesStateHandlerBase semantics) ──────────────
    // KEEP IN SYNC with SalesStateHandlerBase.GetHistory / AddToHistory until R-05
    // collapses both copies into a shared ConversationHistoryStore.

    private List<AiConversationMessage> GetHistory(StateContext ctx)
        => ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();

    private void AddToHistory(StateContext ctx, string role, string content)
    {
        var history = ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();
        history.Add(new AiConversationMessage { Role = role, Content = content, Timestamp = DateTime.UtcNow });

        var limit = _salesBotOptions.ConversationHistoryLimit;
        if (history.Count > limit)
        {
            history = history.Skip(history.Count - limit).ToList();
        }

        ctx.SetData("conversationHistory", history);
    }
}
