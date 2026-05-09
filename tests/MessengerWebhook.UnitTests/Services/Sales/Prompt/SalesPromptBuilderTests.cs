using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.AI.Models;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.Services.SmallTalk.Models;
using MessengerWebhook.Services.Tone.Models;
using MessengerWebhook.StateMachine.Models;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;
using CustomerIntent = MessengerWebhook.Services.AI.Models.CustomerIntent;

namespace MessengerWebhook.UnitTests.Services.Sales.Prompt;

public class SalesPromptBuilderTests
{
    private readonly SalesPromptBuilder _builder;

    public SalesPromptBuilderTests()
    {
        _builder = new SalesPromptBuilder();
    }

    #region BuildCustomerInstruction Tests

    [Fact]
    public void BuildCustomerInstruction_ShouldGreetVip_WhenVipAndShouldGreet()
    {
        // Arrange
        var vipProfile = new VipProfile { IsVip = true, TotalOrders = 5 };

        // Act
        var result = _builder.BuildCustomerInstruction(vipProfile, shouldGreet: true, isReturningCustomer: false);

        // Assert
        Assert.Contains("VIP", result);
        Assert.Contains("khach cu da co 5 don hang", result);
        Assert.Contains("CHAO", result);
    }

    [Fact]
    public void BuildCustomerInstruction_ShouldGreetReturning_WhenReturningAndShouldGreet()
    {
        // Arrange
        var vipProfile = new VipProfile { Tier = VipTier.Returning, TotalOrders = 3 };

        // Act
        var result = _builder.BuildCustomerInstruction(vipProfile, shouldGreet: true, isReturningCustomer: false);

        // Assert
        Assert.Contains("Khach cu", result);
        Assert.Contains("da mua 3 don", result);
        Assert.Contains("CHAO", result);
    }

    [Fact]
    public void BuildCustomerInstruction_ShouldGreetNew_WhenNewAndShouldGreet()
    {
        // Arrange
        var vipProfile = new VipProfile { IsVip = false, Tier = VipTier.Standard };

        // Act
        var result = _builder.BuildCustomerInstruction(vipProfile, shouldGreet: true, isReturningCustomer: false);

        // Assert
        Assert.Contains("Khach moi", result);
        Assert.Contains("CHAO", result);
    }

    [Fact]
    public void BuildCustomerInstruction_ShouldHandleNullVipProfile_WhenGreeting()
    {
        // Act
        var result = _builder.BuildCustomerInstruction(null, shouldGreet: true, isReturningCustomer: false);

        // Assert
        Assert.Contains("Khach moi", result);
    }

    [Fact]
    public void BuildCustomerInstruction_ShouldNotGreetVip_WhenVipAndNoGreeting()
    {
        // Arrange
        var vipProfile = new VipProfile { IsVip = true, TotalOrders = 5 };

        // Act
        var result = _builder.BuildCustomerInstruction(vipProfile, shouldGreet: false, isReturningCustomer: false);

        // Assert
        Assert.Contains("VIP", result);
        Assert.Contains("DA CHAO ROI", result);
        Assert.DoesNotContain("Chao", result.Substring(0, 50));
    }

    [Fact]
    public void BuildCustomerInstruction_ShouldNotGreetReturning_WhenReturningAndNoGreeting()
    {
        // Arrange
        var vipProfile = new VipProfile { Tier = VipTier.Returning, TotalOrders = 2 };

        // Act
        var result = _builder.BuildCustomerInstruction(vipProfile, shouldGreet: false, isReturningCustomer: false);

        // Assert
        Assert.Contains("DA CHAO ROI", result);
        Assert.Contains("CHI TRA LOI", result);
    }

    [Fact]
    public void BuildCustomerInstruction_ShouldNotGreetNew_WhenNewAndNoGreeting()
    {
        // Act
        var result = _builder.BuildCustomerInstruction(null, shouldGreet: false, isReturningCustomer: false);

        // Assert
        Assert.Contains("sau tin chao dau", result);
        Assert.Contains("Chi tra loi", result);
    }

    [Fact]
    public void BuildCustomerInstruction_ShouldConsiderIsReturningCustomer_Flag()
    {
        // Arrange
        VipProfile? vipProfile = null;

        // Act
        var resultReturning = _builder.BuildCustomerInstruction(vipProfile, shouldGreet: true, isReturningCustomer: true);
        var resultNew = _builder.BuildCustomerInstruction(vipProfile, shouldGreet: true, isReturningCustomer: false);

        // Assert
        Assert.Contains("Khach cu", resultReturning);
        Assert.Contains("Khach moi", resultNew);
    }

    #endregion

    #region BuildCtaContext Tests

    [Fact]
    public void BuildCtaContext_ShouldReturnConsultationDeclinedInstruction_WhenConsultationDeclinedAndNoMissingInfo()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("consultationDeclined", true);
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1" });
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.BuildCtaContext(ctx);

        // Assert
        Assert.Contains("declined consultation", result);
        Assert.Contains("Create order immediately", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldAskForMissingInfo_WhenConsultationDeclinedWithMissing()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("consultationDeclined", true);
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1" });
        ctx.SetData("customerPhone", "");
        ctx.SetData("shippingAddress", "");

        // Act
        var result = _builder.BuildCtaContext(ctx);

        // Assert
        Assert.Contains("missing info", result);
        Assert.Contains("so dien thoai", result);
        Assert.Contains("dia chi", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldHandleRejectionThreshold_WhenRejectedTwice()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("consultationRejectionCount", 2);
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1" });

        // Act
        var result = _builder.BuildCtaContext(ctx);

        // Assert
        Assert.Contains("rejected consultation twice", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldAskForConfirmation_WhenNeedsConfirmationAndNoMissing()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("contactNeedsConfirmation", true);
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.BuildCtaContext(ctx, CustomerIntent.ReadyToBuy);

        // Assert
        Assert.Contains("confirm", result);
        Assert.Contains("0912345678", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldNotPushConfirmation_WhenNeedsConfirmationButQuestioning()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("contactNeedsConfirmation", true);
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.BuildCtaContext(ctx, CustomerIntent.Questioning);

        // Assert
        Assert.Contains("consulting", result);
        Assert.Contains("DO NOT push", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldIndicateAllInfoComplete_WhenNoMissingAndNoConfirmationNeeded()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("contactNeedsConfirmation", false);
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.BuildCtaContext(ctx);

        // Assert
        Assert.Contains("all contact information", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldNotPush_WhenEarlyPhaseQuestioning()
    {
        // Arrange: messageCount=1 (only 1 user message), intent=Questioning
        // This triggers the "Customer is in consultation phase" branch at line 72-73
        var ctx = new StateContext();
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "assistant", Content = "Hi there" },
            new() { Role = "user", Content = "Hi" }
        });

        // Act
        var result = _builder.BuildCtaContext(ctx, CustomerIntent.Questioning);

        // Assert
        Assert.Contains("consultation phase", result);
        Assert.Contains("WITHOUT pushing", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldGentlySuggest_WhenThreeToFourMessages()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("conversationHistory", new List<AiConversationMessage>
        {
            new() { Role = "user", Content = "Q1" },
            new() { Role = "assistant", Content = "A1" },
            new() { Role = "user", Content = "Q2" },
            new() { Role = "assistant", Content = "A2" }
        });
        ctx.SetData("selectedProductCodes", new List<string>());

        // Act
        var result = _builder.BuildCtaContext(ctx);

        // Assert
        Assert.Contains("early consultation phase", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldHandleReadyToBuy_WhenMissingInfo()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1" });
        ctx.SetData("customerPhone", "");
        ctx.SetData("shippingAddress", "");

        // Act
        var result = _builder.BuildCtaContext(ctx, CustomerIntent.ReadyToBuy);

        // Assert
        Assert.Contains("ready to buy", result);
        Assert.Contains("missing contact fields", result);
    }

    [Fact]
    public void BuildCtaContext_ShouldAskForMissingContact_WhenHasProduct()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedProductCodes", new List<string> { "PROD1" });
        ctx.SetData("customerPhone", "");

        // Act
        var result = _builder.BuildCtaContext(ctx);

        // Assert
        Assert.Contains("missing info", result);
    }

    #endregion

    #region BuildFactValidationContext Tests

    [Fact]
    public void BuildFactValidationContext_ShouldMapAllFieldsCorrectly()
    {
        // Arrange
        var response = "Test response";
        var toneProfile = new ToneProfile();
        var products = new List<GroundedProduct>
        {
            new(Guid.NewGuid().ToString(), "CODE1", "Product 1", "Cosmetics", 100m),
            new(Guid.NewGuid().ToString(), "CODE2", "Product 2", "Fashion", 200m)
        };

        // Act
        var result = _builder.BuildFactValidationContext(
            response, toneProfile, null, null, "customer msg",
            requiresProductGrounding: true, products,
            allowPolicyFacts: true, allowInventoryFacts: false, allowOrderFacts: true);

        // Assert
        Assert.Equal(response, result.Response);
        Assert.NotNull(result.ToneProfile);
        Assert.NotNull(result.ConversationContext);
        Assert.True(result.RequiresFactGrounding);
        Assert.Contains("Product 1", result.AllowedProductNames);
        Assert.Contains("CODE1", result.AllowedProductCodes);
        Assert.Contains(100m, result.AllowedPrices);
        Assert.True(result.AllowPolicyFacts);
        Assert.False(result.AllowInventoryFacts);
        Assert.True(result.AllowOrderFacts);
    }

    [Fact]
    public void BuildFactValidationContext_ShouldCreateDefaultToneProfile_WhenNull()
    {
        // Act
        var result = _builder.BuildFactValidationContext(
            "response", null, null, null, "msg", false, new List<GroundedProduct>(),
            false, false, false);

        // Assert
        Assert.NotNull(result.ToneProfile);
    }

    [Fact]
    public void BuildFactValidationContext_ShouldCreateDefaultConversationContext_WhenNull()
    {
        // Act
        var result = _builder.BuildFactValidationContext(
            "response", new ToneProfile(), null, null, "msg", false, new List<GroundedProduct>(),
            false, false, false);

        // Assert
        Assert.NotNull(result.ConversationContext);
    }

    [Fact]
    public void BuildFactValidationContext_ShouldFilterOutProductsWithoutPrice()
    {
        // Arrange
        var products = new List<GroundedProduct>
        {
            new(Guid.NewGuid().ToString(), "CODE1", "Product 1", "Cosmetics", 100m),
            new(Guid.NewGuid().ToString(), "CODE2", "Product 2", "Fashion", null)
        };

        // Act
        var result = _builder.BuildFactValidationContext(
            "response", null, null, null, "msg", false, products,
            false, false, false);

        // Assert
        Assert.Single(result.AllowedPrices);
        Assert.Contains(100m, result.AllowedPrices);
    }

    #endregion

    #region FormatAllowedProductNames Tests

    [Fact]
    public void FormatAllowedProductNames_ShouldReturnKhongCo_WhenEmpty()
    {
        // Act
        var result = _builder.FormatAllowedProductNames(new List<GroundedProduct>());

        // Assert
        Assert.Equal("khong co", result);
    }

    [Fact]
    public void FormatAllowedProductNames_ShouldFormatWithCodeInParens_WhenHasProducts()
    {
        // Arrange
        var products = new List<GroundedProduct>
        {
            new(Guid.NewGuid().ToString(), "CODE1", "Product 1", "Cosmetics", 100m),
            new(Guid.NewGuid().ToString(), "CODE2", "Product 2", "Fashion", 200m)
        };

        // Act
        var result = _builder.FormatAllowedProductNames(products);

        // Assert
        Assert.Contains("Product 1 (CODE1)", result);
        Assert.Contains("Product 2 (CODE2)", result);
        Assert.Contains(", ", result);
    }

    #endregion

    #region BuildPolicyGiftMessage Tests

    [Fact]
    public void BuildPolicyGiftMessage_ShouldReturnNoGift_WhenGiftNameEmpty()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedGiftName", "");

        // Act
        var result = _builder.BuildPolicyGiftMessage(ctx);

        // Assert
        Assert.Contains("chưa có quà tặng", result);
    }

    [Fact]
    public void BuildPolicyGiftMessage_ShouldReturnGiftName_WhenProvided()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedGiftName", "Sample Gift");

        // Act
        var result = _builder.BuildPolicyGiftMessage(ctx);

        // Assert
        Assert.Contains("Sample Gift", result);
    }

    [Fact]
    public void BuildPolicyGiftMessage_ShouldHandleNullGiftName()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedGiftName", null);

        // Act
        var result = _builder.BuildPolicyGiftMessage(ctx);

        // Assert
        Assert.Contains("chưa có quà tặng", result);
    }

    #endregion

    #region BuildPendingContactClarificationReply Tests

    [Fact]
    public void BuildPendingContactClarificationReply_ShouldReturnBoth_WhenHasPhoneAndAddress()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.BuildPendingContactClarificationReply(ctx);

        // Assert
        Assert.Contains("0912345678", result);
        Assert.Contains("123 Main St", result);
        Assert.Contains("xác nhận", result);
    }

    [Fact]
    public void BuildPendingContactClarificationReply_ShouldReturnPhoneOnly_WhenHasPhoneOnly()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "");

        // Act
        var result = _builder.BuildPendingContactClarificationReply(ctx);

        // Assert
        Assert.Contains("0912345678", result);
        Assert.Contains("địa chỉ", result);
    }

    [Fact]
    public void BuildPendingContactClarificationReply_ShouldReturnAddressOnly_WhenHasAddressOnly()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.BuildPendingContactClarificationReply(ctx);

        // Assert
        Assert.Contains("123 Main St", result);
        Assert.Contains("điện thoại", result);
    }

    [Fact]
    public void BuildPendingContactClarificationReply_ShouldReturnNeither_WhenNoContact()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "");
        ctx.SetData("shippingAddress", "");

        // Act
        var result = _builder.BuildPendingContactClarificationReply(ctx);

        // Assert
        Assert.Contains("số điện thoại", result);
        Assert.Contains("địa chỉ", result);
    }

    #endregion

    #region GetMissingContactInfo Tests

    [Fact]
    public void GetMissingContactInfo_ShouldReturnBoth_WhenBothMissing()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "");
        ctx.SetData("shippingAddress", "");

        // Act
        var result = _builder.GetMissingContactInfo(ctx);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetMissingContactInfo_ShouldReturnPhoneOnly_WhenPhoneMissing()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.GetMissingContactInfo(ctx);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetMissingContactInfo_ShouldReturnAddressOnly_WhenAddressMissing()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "");

        // Act
        var result = _builder.GetMissingContactInfo(ctx);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public void GetMissingContactInfo_ShouldReturnEmpty_WhenBothProvided()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");

        // Act
        var result = _builder.GetMissingContactInfo(ctx);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    #endregion

    #region BuildDraftConfirmation Tests

    [Fact]
    public void BuildDraftConfirmation_ShouldIncludeGift_WhenGiftProvided()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedGiftName", "Free Gift");
        var draftOrder = new DraftOrder
        {
            DraftCode = "DRAFT001",
            MerchandiseTotal = 500000,
            Items = new List<DraftOrderItem>
            {
                new() { ProductName = "Product 1", Quantity = 1 }
            }
        };

        // Act
        var result = _builder.BuildDraftConfirmation(ctx, draftOrder);

        // Assert
        Assert.Contains("Free Gift", result);
        Assert.Contains("DRAFT001", result);
    }

    [Fact]
    public void BuildDraftConfirmation_ShouldNotIncludeGift_WhenNoGift()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedGiftName", "");
        var draftOrder = new DraftOrder
        {
            DraftCode = "DRAFT001",
            MerchandiseTotal = 500000,
            Items = new List<DraftOrderItem>
            {
                new() { ProductName = "Product 1", Quantity = 1 }
            }
        };

        // Act
        var result = _builder.BuildDraftConfirmation(ctx, draftOrder);

        // Assert
        Assert.DoesNotContain("Qua tang", result);
    }

    [Fact]
    public void BuildDraftConfirmation_ShouldIncludeSaveContact_WhenFlagSet()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedGiftName", "");
        ctx.SetData("currentOrderUsesUpdatedContact", true);
        ctx.SetData("pendingContactQuestion", "ask_save_new_contact");
        var draftOrder = new DraftOrder
        {
            DraftCode = "DRAFT001",
            MerchandiseTotal = 500000,
            Items = new List<DraftOrderItem>()
        };

        // Act
        var result = _builder.BuildDraftConfirmation(ctx, draftOrder);

        // Assert
        Assert.Contains("cập nhật", result);
    }

    [Fact]
    public void BuildDraftConfirmation_ShouldShowItemDetails_WhenItemsPresent()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("selectedGiftName", "");
        var draftOrder = new DraftOrder
        {
            DraftCode = "DRAFT001",
            MerchandiseTotal = 500000,
            Items = new List<DraftOrderItem>
            {
                new() { ProductName = "Product 1", Quantity = 2 },
                new() { ProductName = "Product 2", Quantity = 1 }
            }
        };

        // Act
        var result = _builder.BuildDraftConfirmation(ctx, draftOrder);

        // Assert
        Assert.Contains("Product 1 x2", result);
        Assert.Contains("Product 2", result);
    }

    #endregion

    #region NormalizeSentence Tests

    [Fact]
    public void NormalizeSentence_ShouldReturnDefault_WhenNull()
    {
        // Act
        var result = _builder.NormalizeSentence(null);

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("sản phẩm", result);
    }

    [Fact]
    public void NormalizeSentence_ShouldReturnDefault_WhenEmpty()
    {
        // Act
        var result = _builder.NormalizeSentence("   ");

        // Assert
        Assert.NotEmpty(result);
        Assert.Contains("sản phẩm", result);
    }

    [Fact]
    public void NormalizeSentence_ShouldEndWithPeriod_WhenEndsWithA()
    {
        // Act
        var result = _builder.NormalizeSentence("Test ạ");

        // Assert
        Assert.EndsWith(".", result);
        Assert.Contains("ạ.", result);
    }

    [Fact]
    public void NormalizeSentence_ShouldAddAAndPeriod_WhenNoEnding()
    {
        // Act
        var result = _builder.NormalizeSentence("Test");

        // Assert
        Assert.EndsWith("ạ.", result);
    }

    [Fact]
    public void NormalizeSentence_ShouldRemoveExistingPunctuation()
    {
        // Act
        var result = _builder.NormalizeSentence("Test!");

        // Assert
        Assert.DoesNotContain("!", result);
        Assert.Contains("ạ.", result);
    }

    [Fact]
    public void NormalizeSentence_ShouldTrimWhitespace()
    {
        // Act
        var result = _builder.NormalizeSentence("   Test   ");

        // Assert
        Assert.Equal("Test ạ.", result);
    }

    #endregion

    #region GetContactSummary Tests

    [Fact]
    public void GetContactSummary_ShouldShowBothProvided_WhenHasPhoneAndAddress()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");
        ctx.SetData("contactNeedsConfirmation", false);

        // Act
        var result = _builder.GetContactSummary(ctx);

        // Assert
        Assert.Contains("SDT=da co", result);
        Assert.Contains("Dia chi=da co", result);
    }

    [Fact]
    public void GetContactSummary_ShouldShowNeedingConfirmation_WhenFlagSet()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "0912345678");
        ctx.SetData("shippingAddress", "123 Main St");
        ctx.SetData("contactNeedsConfirmation", true);

        // Act
        var result = _builder.GetContactSummary(ctx);

        // Assert
        Assert.Contains("dang nho lai", result);
    }

    [Fact]
    public void GetContactSummary_ShouldShowMissing_WhenNotProvided()
    {
        // Arrange
        var ctx = new StateContext();
        ctx.SetData("customerPhone", "");
        ctx.SetData("shippingAddress", "");
        ctx.SetData("contactNeedsConfirmation", false);

        // Act
        var result = _builder.GetContactSummary(ctx);

        // Assert
        Assert.Contains("chua co", result);
    }

    #endregion

    #region DetermineNextState Tests

    [Theory]
    [InlineData(CustomerIntent.Browsing, ConversationState.Consulting)]
    [InlineData(CustomerIntent.Consulting, ConversationState.Consulting)]
    [InlineData(CustomerIntent.Questioning, ConversationState.Consulting)]
    public void DetermineNextState_ShouldReturnConsulting_ForQuestioningIntents(
        CustomerIntent intent, ConversationState expected)
    {
        // Act
        var result = _builder.DetermineNextState(intent, hasProduct: true, hasContact: false);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DetermineNextState_ShouldReturnCollecting_WhenReadyToBuyWithProduct()
    {
        // Act
        var result = _builder.DetermineNextState(CustomerIntent.ReadyToBuy, hasProduct: true, hasContact: false);

        // Assert
        Assert.Equal(ConversationState.CollectingInfo, result);
    }

    [Fact]
    public void DetermineNextState_ShouldReturnConsulting_WhenReadyToBuyNoProduct()
    {
        // Act
        var result = _builder.DetermineNextState(CustomerIntent.ReadyToBuy, hasProduct: false, hasContact: false);

        // Assert
        Assert.Equal(ConversationState.Consulting, result);
    }

    [Fact]
    public void DetermineNextState_ShouldReturnCollecting_WhenConfirming()
    {
        // Act
        var result = _builder.DetermineNextState(CustomerIntent.Confirming, hasProduct: false, hasContact: false);

        // Assert
        Assert.Equal(ConversationState.CollectingInfo, result);
    }

    #endregion
}
