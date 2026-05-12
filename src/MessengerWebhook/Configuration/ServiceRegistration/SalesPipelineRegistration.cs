using MessengerWebhook.Services;
using MessengerWebhook.Services.ABTesting;
using MessengerWebhook.Services.ABTesting.Configuration;
using MessengerWebhook.Services.AI.Handlers;
using MessengerWebhook.Services.Conversation;
using MessengerWebhook.Services.Conversation.Configuration;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.DraftOrders;
using MessengerWebhook.Services.Emotion;
using MessengerWebhook.Services.Emotion.Configuration;
using MessengerWebhook.Services.Freeship;
using MessengerWebhook.Services.GiftSelection;
using MessengerWebhook.Services.Metrics;
using MessengerWebhook.Services.Metrics.Configuration;
using MessengerWebhook.Services.Policy;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ProductMapping;
using MessengerWebhook.Services.ResponseValidation;
using MessengerWebhook.Services.ResponseValidation.Configuration;
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.Sales.Reply;
using MessengerWebhook.Services.SmallTalk;
using MessengerWebhook.Services.SmallTalk.Configuration;
using MessengerWebhook.Services.SubIntent;
using MessengerWebhook.Services.Support;
using MessengerWebhook.Services.Support.EmailTemplates;
using MessengerWebhook.Services.Survey;
using MessengerWebhook.Services.Tone;
using MessengerWebhook.Services.Tone.Configuration;
using MessengerWebhook.StateMachine;
using MessengerWebhook.StateMachine.Handlers;
using Microsoft.Extensions.Options;

namespace MessengerWebhook.Configuration.ServiceRegistration;

internal static class SalesPipelineRegistration
{
    internal static IServiceCollection AddSalesPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        // Core sales + support options
        services.Configure<SalesBotOptions>(configuration.GetSection(SalesBotOptions.SectionName));
        services.Configure<SupportOptions>(configuration.GetSection(SupportOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        // Policy guard
        services.Configure<PolicyGuardOptions>(configuration.GetSection(PolicyGuardOptions.SectionName));
        services.AddSingleton<IValidateOptions<PolicyGuardOptions>, ValidatePolicyGuardOptions>();
        services.AddOptions<PolicyGuardOptions>().ValidateOnStart();
        services.AddScoped<IPolicyMessageNormalizer, DefaultPolicyMessageNormalizer>();
        services.AddScoped<IPolicyRiskScorer, DefaultPolicyRiskScorer>();
        services.AddScoped<IPolicySignalDetector, KeywordPolicySignalDetector>();
        services.AddScoped<IPolicySignalDetector, RegexPolicySignalDetector>();
        services.AddScoped<IPolicySignalDetector, FuzzyPolicySignalDetector>();
        services.AddScoped<IPolicyGuardService, PolicyGuardService>();
        services.AddScoped<IRiskMessageSanitizer, RiskMessageSanitizer>();
        services.AddHttpClient<IPolicyIntentClassifier, GeminiPolicyIntentClassifier>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            })
            .AddHttpMessageHandler<GeminiAuthHandler>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddStandardResilienceHandler();

        // Emotion detection
        services.Configure<EmotionDetectionOptions>(configuration.GetSection("EmotionDetection"));
        services.AddSingleton<IValidateOptions<EmotionDetectionOptions>, ValidateEmotionDetectionOptions>();
        services.AddOptions<EmotionDetectionOptions>().ValidateOnStart();
        services.AddScoped<IEmotionDetectionService, EmotionDetectionService>();

        // Tone matching
        services.Configure<ToneMatchingOptions>(configuration.GetSection("ToneMatching"));
        services.AddSingleton<IValidateOptions<ToneMatchingOptions>, ValidateToneMatchingOptions>();
        services.AddOptions<ToneMatchingOptions>().ValidateOnStart();
        services.AddScoped<IToneMatchingService, ToneMatchingService>();

        // Conversation context analyzer
        services.Configure<ConversationAnalysisOptions>(configuration.GetSection("ConversationAnalysis"));
        services.AddSingleton<IValidateOptions<ConversationAnalysisOptions>, ValidateConversationAnalysisOptions>();
        services.AddOptions<ConversationAnalysisOptions>().ValidateOnStart();
        services.AddScoped<PatternDetector>();
        services.AddScoped<TopicAnalyzer>();
        services.AddScoped<IConversationContextAnalyzer, ConversationContextAnalyzer>();

        // Small talk
        services.Configure<SmallTalkOptions>(configuration.GetSection("SmallTalk"));
        services.AddSingleton<IValidateOptions<SmallTalkOptions>, ValidateSmallTalkOptions>();
        services.AddOptions<SmallTalkOptions>().ValidateOnStart();
        services.AddSingleton<SmallTalkDetector>();
        services.AddSingleton<ISmallTalkService, SmallTalkService>();

        // Response validation
        services.Configure<ResponseValidationOptions>(configuration.GetSection("ResponseValidation"));
        services.AddSingleton<IValidateOptions<ResponseValidationOptions>, ValidateResponseValidationOptions>();
        services.AddOptions<ResponseValidationOptions>().ValidateOnStart();
        services.AddSingleton<IResponseValidationService, ResponseValidationService>();

        // A/B testing
        services.Configure<ABTestingOptions>(configuration.GetSection(ABTestingOptions.SectionName));
        services.AddSingleton<IValidateOptions<ABTestingOptions>, ValidateABTestingOptions>();
        services.AddOptions<ABTestingOptions>().ValidateOnStart();
        services.AddScoped<IABTestService, ABTestService>();

        // Metrics + CSAT
        services.Configure<MetricsOptions>(configuration.GetSection(MetricsOptions.SectionName));
        services.AddSingleton<IConversationMetricsService, ConversationMetricsService>();
        services.AddScoped<IMetricsAggregationService, MetricsAggregationService>();
        services.Configure<CSATSurveyOptions>(configuration.GetSection("CSATSurvey"));
        services.AddScoped<ICSATSurveyService, CSATSurveyService>();

        // SubIntent classification
        services.Configure<SubIntentOptions>(configuration.GetSection(SubIntentOptions.SectionName));
        services.AddSingleton<KeywordSubIntentDetector>();
        services.AddHttpClient<GeminiSubIntentClassifier>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            })
            .AddHttpMessageHandler<GeminiAuthHandler>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5))
            .AddStandardResilienceHandler();
        services.AddScoped<ISubIntentClassifier, HybridSubIntentClassifier>();

        // Core domain services
        services.AddScoped<IBotLockService, BotLockService>();
        services.AddScoped<ICaseEscalationService, CaseEscalationService>();
        services.AddScoped<ICustomerIntelligenceService, CustomerIntelligenceService>();
        services.AddScoped<IDraftOrderService, DraftOrderService>();
        services.AddScoped<DraftOrderCoordinator>();
        services.AddScoped<ISupportCaseManagementService, SupportCaseManagementService>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<ISupportCaseTokenService, SupportCaseTokenService>();

        // Product pipeline
        services.AddScoped<IProductMappingService, ProductMappingService>();
        services.AddScoped<IProductNeedDetector, ProductNeedDetector>();
        services.AddScoped<IProductMentionDetector, ProductMentionDetector>();
        services.AddScoped<IProductGroundingService, ProductGroundingService>();
        services.AddScoped<IGiftSelectionService, GiftSelectionService>();
        services.AddScoped<IFreeshipCalculator, FreeshipCalculator>();

        // R-02..R-05 extracted sales services
        services.AddScoped<ISalesContextResolver, SalesContextResolver>();
        services.AddSingleton<ISalesPromptBuilder, SalesPromptBuilder>();
        services.AddScoped<IContactConfirmationFlow, ContactConfirmationFlow>();
        services.AddScoped<ISalesReplyOrchestrator, SalesReplyOrchestrator>();
        services.AddScoped<ISalesConsultationReplies, SalesConsultationReplies>();

        // State machine
        services.AddScoped<IStateMachine, ConversationStateMachine>();
        services.AddScoped<IStateHandler, IdleStateHandler>();
        services.AddScoped<IStateHandler, QuickReplySalesStateHandler>();
        services.AddScoped<IStateHandler, ConsultingStateHandler>();
        services.AddScoped<IStateHandler, CollectingInfoStateHandler>();
        services.AddScoped<IStateHandler, DraftOrderStateHandler>();
        services.AddScoped<IStateHandler, CompleteStateHandler>();
        services.AddScoped<IStateHandler, HumanHandoffStateHandler>();
        services.AddScoped<IStateHandler, GreetingStateHandler>();
        services.AddScoped<IStateHandler, MainMenuStateHandler>();
        services.AddScoped<IStateHandler, BrowsingProductsStateHandler>();
        services.AddScoped<IStateHandler, ProductDetailStateHandler>();
        services.AddScoped<IStateHandler, VariantSelectionStateHandler>();
        services.AddScoped<IStateHandler, AddToCartStateHandler>();
        services.AddScoped<IStateHandler, CartReviewStateHandler>();
        services.AddScoped<IStateHandler, ShippingAddressStateHandler>();
        services.AddScoped<IStateHandler, PaymentMethodStateHandler>();
        services.AddScoped<IStateHandler, OrderConfirmationStateHandler>();
        services.AddScoped<IStateHandler, OrderPlacedStateHandler>();
        services.AddScoped<IStateHandler, OrderTrackingStateHandler>();
        services.AddScoped<IStateHandler, SkinAnalysisStateHandler>();
        services.AddScoped<IStateHandler, SkinConsultationStateHandler>();
        services.AddScoped<IStateHandler, HelpStateHandler>();
        services.AddScoped<IStateHandler, ErrorStateHandler>();

        return services;
    }
}
