using FluentAssertions;
using MessengerWebhook.Data.Entities;
using MessengerWebhook.Services.Sales.Contact;
using MessengerWebhook.Services.Sales.Context;
using MessengerWebhook.Services.Sales.Prompt;
using MessengerWebhook.StateMachine.Models;
using Moq;

namespace MessengerWebhook.UnitTests.Services.Sales.Contact;

/// <summary>
/// Unit tests for ContactConfirmationFlow.
/// Covers truth table: contact state × message type → expected decision.
/// </summary>
public class ContactConfirmationFlowTests
{
    private readonly Mock<ISalesContextResolver> _resolver = new();
    private readonly ISalesPromptBuilder _promptBuilder = new SalesPromptBuilder();
    private readonly ContactConfirmationFlow _flow;

    public ContactConfirmationFlowTests()
    {
        _flow = new ContactConfirmationFlow(_resolver.Object, _promptBuilder);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StateContext BuildCtx(
        bool contactNeedsConfirmation = false,
        string? pendingContactQuestion = null,
        string? customerPhone = null,
        string? shippingAddress = null)
    {
        var ctx = new StateContext { FacebookPSID = "test-psid" };
        ctx.SetData("contactNeedsConfirmation", contactNeedsConfirmation ? (bool?)true : null);
        ctx.SetData("pendingContactQuestion", pendingContactQuestion);
        ctx.SetData("customerPhone", customerPhone);
        ctx.SetData("shippingAddress", shippingAddress);
        return ctx;
    }

    private static Product MakeProduct(string name = "Kem Lụa") =>
        new() { Id = Guid.NewGuid().ToString(), Code = "KL001", Name = name, TenantId = Guid.NewGuid() };

    // ── IsContactMemoryQuestion ───────────────────────────────────────────────

    [Theory]
    [InlineData("có thông tin của chị chưa", true)]
    [InlineData("em có thông tin của chị chưa", true)]
    [InlineData("có số của chị chưa", true)]
    [InlineData("có địa chỉ của chị chưa", true)]
    [InlineData("em có số điện thoại của chị chưa", true)]
    [InlineData("co thong tin cua chi chua", true)]
    [InlineData("em co thong tin cua chi chua", true)]
    [InlineData("co so cua chi chua", true)]
    [InlineData("co dia chi cua chi chua", true)]
    [InlineData("em co so dien thoai cua chi chua", true)]
    public void IsContactMemoryQuestion_MatchingPhrases_ReturnsTrue(string message, bool expected)
    {
        _flow.IsContactMemoryQuestion(message).Should().Be(expected);
    }

    [Theory]
    [InlineData("mua sản phẩm này", false)]
    [InlineData("ok em", false)]
    [InlineData("thông tin giao hàng", false)]
    [InlineData("chị muốn chốt", false)]
    [InlineData("", false)]
    public void IsContactMemoryQuestion_NonMatchingPhrases_ReturnsFalse(string message, bool expected)
    {
        _flow.IsContactMemoryQuestion(message).Should().Be(expected);
    }

    // ── IsPendingClarificationQuestion ────────────────────────────────────────

    [Theory]
    [InlineData("thông tin nào", true)]
    [InlineData("thong tin nao", true)]
    [InlineData("thông tin gì", true)]
    [InlineData("thong tin gi", true)]
    [InlineData("xác nhận thông tin nào", true)]
    [InlineData("xac nhan thong tin nao", true)]
    public void IsPendingClarificationQuestion_AwaitingConfirmationAndClarificationPhrase_ReturnsTrue(
        string message, bool expected)
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "confirm_old_contact");
        _flow.IsPendingClarificationQuestion(ctx, message).Should().Be(expected);
    }

    [Fact]
    public void IsPendingClarificationQuestion_NotAwaitingConfirmation_ReturnsFalse()
    {
        var ctx = BuildCtx(contactNeedsConfirmation: false);
        _flow.IsPendingClarificationQuestion(ctx, "thông tin nào").Should().BeFalse();
    }

    [Fact]
    public void IsPendingClarificationQuestion_WrongPendingQuestion_ReturnsFalse()
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "ask_save_new_contact");
        _flow.IsPendingClarificationQuestion(ctx, "thông tin nào").Should().BeFalse();
    }

    [Fact]
    public void IsPendingClarificationQuestion_AwaitingButNonClarificationMessage_ReturnsFalse()
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "confirm_old_contact");
        _flow.IsPendingClarificationQuestion(ctx, "ok chốt đơn cho em").Should().BeFalse();
    }

    // ── IsGenericBuyContinuationPendingConfirmation ───────────────────────────

    [Theory]
    [InlineData("ok")]
    [InlineData("oke")]
    [InlineData("okay")]
    [InlineData("ok e")]
    [InlineData("ok em")]
    [InlineData("oke e")]
    [InlineData("oke em")]
    [InlineData("lên đơn cho em")]
    [InlineData("chốt đơn nhé")]
    [InlineData("đặt hàng luôn")]
    [InlineData("mua luôn")]
    [InlineData("lấy sản phẩm này")]
    [InlineData("lấy nha")]
    [InlineData("lấy nhé")]
    public void IsGenericBuyContinuationPendingConfirmation_BuySignalWhileAwaiting_ReturnsTrue(string message)
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "confirm_old_contact");
        _flow.IsGenericBuyContinuationPendingConfirmation(ctx, message).Should().BeTrue();
    }

    [Theory]
    [InlineData("dùng rồi")]           // confirmation phrase — NOT a buy signal
    [InlineData("vẫn dùng")]           // confirmation phrase
    [InlineData("như cũ")]             // confirmation phrase
    [InlineData("thông tin cũ")]       // confirmation phrase
    [InlineData("cũ như vậy")]         // confirmation phrase
    [InlineData("mua sản phẩm này")]   // buy phrase but NOT in rejection guard — tests rejection guard scoping
    public void IsGenericBuyContinuationPendingConfirmation_ConfirmationPhrases_ReturnsFalse(string message)
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "confirm_old_contact");
        // Phrases in rejection guard → false; others: depends on exact match
        // "mua san pham nay" doesn't match any buy signal → false
        _flow.IsGenericBuyContinuationPendingConfirmation(ctx, message).Should().BeFalse();
    }

    [Fact]
    public void IsGenericBuyContinuationPendingConfirmation_NotAwaitingConfirmation_ReturnsFalse()
    {
        var ctx = BuildCtx(contactNeedsConfirmation: false);
        _flow.IsGenericBuyContinuationPendingConfirmation(ctx, "ok em").Should().BeFalse();
    }

    [Fact]
    public void IsGenericBuyContinuationPendingConfirmation_WrongPendingQuestion_ReturnsFalse()
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "ask_save_new_contact");
        _flow.IsGenericBuyContinuationPendingConfirmation(ctx, "ok").Should().BeFalse();
    }

    [Fact]
    public void IsGenericBuyContinuationPendingConfirmation_EmptyMessage_ReturnsFalse()
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "confirm_old_contact");
        _flow.IsGenericBuyContinuationPendingConfirmation(ctx, "").Should().BeFalse();
    }

    // ── BuildContactMemoryReplyAsync ──────────────────────────────────────────

    [Fact]
    public async Task BuildContactMemoryReplyAsync_AllInfoWithConfirmation_ReturnsConfirmRequest()
    {
        var ctx = BuildCtx(true, "confirm_old_contact", "0901234567", "123 Lê Lợi, Q1");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactMemoryReplyAsync(ctx, "em có thông tin chưa");

        result.Should().Contain("0901234567");
        result.Should().Contain("123 Lê Lợi, Q1");
        result.Should().Contain("xác nhận");
    }

    [Fact]
    public async Task BuildContactMemoryReplyAsync_AllInfoWithConfirmationAndProduct_IncludesProductName()
    {
        var ctx = BuildCtx(true, "confirm_old_contact", "0901234567", "123 Lê Lợi");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync(MakeProduct("Kem Lụa"));

        var result = await _flow.BuildContactMemoryReplyAsync(ctx, "em có thông tin chưa");

        result.Should().Contain("Kem Lụa");
        result.Should().Contain("0901234567");
    }

    [Fact]
    public async Task BuildContactMemoryReplyAsync_AllInfoConfirmed_ReturnsReadyMessage()
    {
        var ctx = BuildCtx(false, null, "0901234567", "123 Lê Lợi, Q1");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactMemoryReplyAsync(ctx, "em có thông tin chưa");

        result.Should().Contain("đủ thông tin");
        result.Should().NotContain("xác nhận");
    }

    [Fact]
    public async Task BuildContactMemoryReplyAsync_MissingPhone_ReturnsMissingRequest()
    {
        var ctx = BuildCtx(false, null, null, "123 Lê Lợi, Q1");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactMemoryReplyAsync(ctx, "em có thông tin chưa");

        result.Should().Contain("số điện thoại");
    }

    [Fact]
    public async Task BuildContactMemoryReplyAsync_MissingAddress_ReturnsMissingRequest()
    {
        var ctx = BuildCtx(false, null, "0901234567", null);
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactMemoryReplyAsync(ctx, "em có thông tin chưa");

        result.Should().Contain("địa chỉ");
    }

    [Fact]
    public async Task BuildContactMemoryReplyAsync_AllInfoConfirmedWithProduct_ReturnsProductAwareMessage()
    {
        var ctx = BuildCtx(false, null, "0901234567", "123 Lê Lợi");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync(MakeProduct("Serum VB"));

        var result = await _flow.BuildContactMemoryReplyAsync(ctx, "em có thông tin chưa");

        result.Should().Contain("Serum VB");
        result.Should().Contain("đủ thông tin");
    }

    // ── BuildContactCollectionReplyAsync ─────────────────────────────────────

    [Fact]
    public async Task BuildContactCollectionReplyAsync_ConfirmationPendingAllInfoPresent_ReturnsConfirmRequest()
    {
        var ctx = BuildCtx(true, "confirm_old_contact", "0901234567", "123 Lê Lợi, Q1");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().NotBeNull();
        result.Should().Contain("0901234567");
        result.Should().Contain("123 Lê Lợi, Q1");
        result.Should().Contain("xác nhận");
    }

    [Fact]
    public async Task BuildContactCollectionReplyAsync_ConfirmationPendingWithProduct_IncludesProduct()
    {
        var ctx = BuildCtx(true, "confirm_old_contact", "0901234567", "123 Lê Lợi");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync(MakeProduct("Toner X"));

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().Contain("Toner X");
        result.Should().Contain("0901234567");
    }

    [Fact]
    public async Task BuildContactCollectionReplyAsync_AllInfoConfirmed_ReturnsNull()
    {
        var ctx = BuildCtx(false, null, "0901234567", "123 Lê Lợi, Q1");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildContactCollectionReplyAsync_MissingPhone_AskForPhone()
    {
        var ctx = BuildCtx(false, null, null, "123 Lê Lợi, Q1");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().Contain("số điện thoại");
    }

    [Fact]
    public async Task BuildContactCollectionReplyAsync_MissingAddress_AskForAddress()
    {
        var ctx = BuildCtx(false, null, "0901234567", null);
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().Contain("địa chỉ");
    }

    [Fact]
    public async Task BuildContactCollectionReplyAsync_MissingBothWithProduct_AskForBothAndMentionProduct()
    {
        var ctx = BuildCtx(false, null, null, null);
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync(MakeProduct("Kem Mật Ong"));

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().Contain("Kem Mật Ong");
        result.Should().Contain("số điện thoại");
        result.Should().Contain("địa chỉ");
    }

    [Fact]
    public async Task BuildContactCollectionReplyAsync_ConfirmationPendingButMissingPhone_AskForPhone()
    {
        // needsConfirmation=true but phone absent → asks for missing field, not confirmation
        var ctx = BuildCtx(true, "confirm_old_contact", null, "123 Lê Lợi, Q1");
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().Contain("số điện thoại");
        result.Should().NotContain("xác nhận");
    }

    [Fact]
    public async Task BuildContactCollectionReplyAsync_ConfirmationPendingButMissingAddress_AskForAddress()
    {
        // needsConfirmation=true but address absent → asks for missing field, not confirmation
        var ctx = BuildCtx(true, "confirm_old_contact", "0901234567", null);
        _resolver.Setup(r => r.GetActiveProductOrResolveAsync(ctx, It.IsAny<string>()))
            .ReturnsAsync((Product?)null);

        var result = await _flow.BuildContactCollectionReplyAsync(ctx, "chốt đơn");

        result.Should().Contain("địa chỉ");
        result.Should().NotContain("xác nhận");
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void IsGenericBuyContinuationPendingConfirmation_OkWithConfirmationSuffix_StillTrue()
    {
        // "ok em" is an exact-match buy signal even when state is pending
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "confirm_old_contact");
        _flow.IsGenericBuyContinuationPendingConfirmation(ctx, "OK EM").Should().BeTrue();
    }

    [Fact]
    public void IsPendingClarificationQuestion_CaseInsensitive_ReturnsTrue()
    {
        var ctx = BuildCtx(contactNeedsConfirmation: true, pendingContactQuestion: "confirm_old_contact");
        _flow.IsPendingClarificationQuestion(ctx, "THÔNG TIN NÀO").Should().BeTrue();
    }

    [Fact]
    public void IsContactMemoryQuestion_CaseInsensitive_ReturnsTrue()
    {
        _flow.IsContactMemoryQuestion("CÓ THÔNG TIN CỦA CHỊ CHƯA").Should().BeTrue();
    }
}
