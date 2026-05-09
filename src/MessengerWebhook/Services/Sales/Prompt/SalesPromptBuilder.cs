using MessengerWebhook.Data.Entities;
using MessengerWebhook.Models;
using MessengerWebhook.Services.Conversation.Models;
using MessengerWebhook.Services.Customers;
using MessengerWebhook.Services.ProductGrounding;
using MessengerWebhook.Services.ResponseValidation.Models;
using MessengerWebhook.Services.SmallTalk.Models;
using MessengerWebhook.Services.Tone.Models;
using MessengerWebhook.StateMachine.Models;
using MessengerWebhook.Utilities;
using AiConversationMessage = MessengerWebhook.Services.AI.Models.ConversationMessage;
using CustomerIntent = MessengerWebhook.Services.AI.Models.CustomerIntent;

namespace MessengerWebhook.Services.Sales.Prompt;

/// <summary>
/// Pure prompt-string builders for the sales conversation pipeline.
/// No async, no side effects — all methods return strings from input data.
/// </summary>
public sealed class SalesPromptBuilder : ISalesPromptBuilder
{
    public string BuildCustomerInstruction(VipProfile? vipProfile, bool shouldGreet, bool isReturningCustomer)
    {
        if (shouldGreet)
        {
            if (vipProfile?.IsVip == true)
                return $"Khach hang VIP (khach cu da co {vipProfile.TotalOrders} don hang):\n- Day la tin nhan dau tien cua khach trong cuoc hoi thoai nay\n- Chao hoi am ap, than mat, tu nhien nhu cham soc khach quen\n- KHONG gioi thieu lai san pham hoac page\n- SAU LOI CHAO BAT BUOC PHAI CO 1 CAU CHUYEN TIEP hoi nhu cau hien tai\n- Co the dung mau: \"Hom nay chi dang can em tu van gi a?\" hoac \"Dot nay chi dang quan tam san pham nao a?\"\n- CHI dung xung ho than thien 1 lan o tin nhan chao dau tien";
            if (vipProfile?.Tier == VipTier.Returning || isReturningCustomer)
                return $"Khach cu (da mua {vipProfile?.TotalOrders ?? 0} don):\n- Day la tin nhan dau tien cua khach trong cuoc hoi thoai nay\n- Chao nhe nhang, than thien, tu nhien hon kieu mau may moc\n- KHONG gioi thieu lai catalog\n- SAU LOI CHAO BAT BUOC PHAI CO 1 CAU CHUYEN TIEP hoi khach dang can gi de tu van\n- Co the dung mau: \"Hom nay chi dang can em tu van gi a?\" hoac \"Chi dang tim san pham nao de em goi y nhanh a?\"\n- Tin nhan chao phai vua co loi chao vua co cau hoi nhu cau, khong duoc chao xong bo ngo";
            return "Khach moi - tin nhan dau tien:\n- Chao tu nhien, mem va giong nhan vien cham soc khach hang that\n- KHONG chao kho cung, KHONG chi chao roi dung lai\n- SAU LOI CHAO BAT BUOC PHAI CO 1 CAU CHUYEN TIEP hoi khach dang can gi de tu van\n- Co the dung mau: \"Dạ em chào chị, hôm nay chị đang cần em tư vấn gì ạ?\" hoac \"Chào chị nha, chị đang quan tâm sản phẩm nào để em tư vấn nhanh cho mình ạ?\"\n- Khong tu y gioi thieu dai dong catalog neu khach chi moi chao";
        }

        if (vipProfile?.IsVip == true)
            return $"Khach hang VIP (da mua {vipProfile.TotalOrders} don) - DA CHAO ROI:\n- KHONG chao lai\n- Chi tra loi cau hoi va ho tro khach ngan gon, tu nhien\n- CTA bien the hoa, khong lap lai cung cau hoi lien tiep";
        if (vipProfile?.Tier == VipTier.Returning || isReturningCustomer)
            return "Khach cu - DA CHAO ROI:\n- CHI TRA LOI CAU HOI, khong chao lai\n- Dung giong binh thuong, ngan gon, tu nhien\n- CTA bien the hoa neu can, tranh lap lai cung 1 cau hoi";
        return "Khach moi sau tin chao dau:\n- Chi tra loi dung cau hoi hien tai, khong chao lai\n- Giong tu nhien, ngan gon, ro y";
    }

    public string BuildCtaContext(StateContext ctx, CustomerIntent? intent = null)
    {
        var hasProduct = (ctx.GetData<List<string>>("selectedProductCodes") ?? new List<string>()).Count > 0;
        var rejectionCount = ctx.GetData<int>("consultationRejectionCount");
        var consultationDeclined = ctx.GetData<bool?>("consultationDeclined") == true;
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;
        var missingInfo = GetMissingContactInfo(ctx);
        var history = ctx.GetData<List<AiConversationMessage>>("conversationHistory") ?? new List<AiConversationMessage>();
        var messageCount = history.Count(m => m.Role == "user");

        if (consultationDeclined && hasProduct)
        {
            if (missingInfo.Count == 0) return "CTA Instruction: Customer declined consultation. Create order immediately. Use: \"Vậy là mình chốt đơn này luôn nha chị. Em lên đơn ngay.\"";
            var missing = string.Join(" va ", missingInfo);
            return $"CTA Instruction: Customer declined consultation. Ask for missing info ({missing}) to complete order. DO NOT ask about consultation again.";
        }

        if (rejectionCount >= 2 && hasProduct)
            return "CTA Instruction: Customer rejected consultation twice. DO NOT ask again. Move to order closing or ask for missing contact info.";

        if (needsConfirmation && missingInfo.Count == 0)
        {
            var phone = ctx.GetData<string>("customerPhone");
            var address = ctx.GetData<string>("shippingAddress");
            if (intent == CustomerIntent.ReadyToBuy || intent == CustomerIntent.Confirming)
                return $"CTA Instruction: Customer is returning and already wants to buy. Ask them to confirm their existing phone and address before creating the order.\n- Use concise wording like: \"Chi xac nhan giup em SDT {phone} va dia chi {address} con dung khong a?\"\n- DO NOT say the order is being created yet.";
            return $"CTA Instruction: Customer is still consulting. We have previous contact info (SDT {phone}, dia chi {address}) but DO NOT push for confirmation yet.\n- Answer their question first\n- No closing CTA while they are still asking";
        }

        if (missingInfo.Count == 0 && !needsConfirmation)
            return "CTA Instruction: Customer already provided and confirmed all contact information.\n- If they are explicitly buying, tell them you will create the order now.\n- If they are still asking questions, continue answering and do NOT add closing CTA.";

        if (messageCount <= 2 && intent == CustomerIntent.Questioning)
            return "CTA Instruction: Customer is in consultation phase (asking questions). Answer naturally WITHOUT pushing for order.\n- Just answer the question directly\n- DO NOT add CTA like \"Chị chọn sản phẩm và gửi thông tin\"\n- Let customer continue asking questions naturally";

        if (messageCount >= 3 && messageCount <= 4 && !hasProduct)
            return "CTA Instruction: Customer has asked 3-4 questions. Gently suggest next step.\n- Use soft prompts like \"Chị quan tâm mẫu này ạ?\" or \"Chị muốn em tư vấn thêm gì không ạ?\"\n- DO NOT push hard for order yet";

        if (intent == CustomerIntent.ReadyToBuy && hasProduct)
        {
            var missing = string.Join(" va ", missingInfo);
            return $"CTA Instruction: Customer is ready to buy.\n- Ask only for missing contact fields or ask to confirm remembered contact.\n- DO NOT use broad closing lines like \"len don ngay\" before contact is complete and confirmed.\n- Missing info: {missing}";
        }

        if (hasProduct && missingInfo.Count > 0)
        {
            var missing = string.Join(" va ", missingInfo);
            return $"CTA Instruction: Naturally ask customer to provide missing info ({missing}) to complete the order. Use friendly tone like \"Chi gui em {missing} de em len don nha\" or \"Em can {missing} cua chi de len don a\".";
        }

        if (messageCount >= 3)
            return "CTA Instruction: Naturally ask customer to choose a product. Use friendly tone like \"Chị quan tâm sản phẩm nào ạ?\" or \"Chị muốn em tư vấn thêm không ạ?\".";

        return "CTA Instruction: Customer is in early consultation phase. Answer questions naturally WITHOUT CTA.";
    }

    public ResponseValidationContext BuildFactValidationContext(
        string response, ToneProfile? toneProfile, ConversationContext? conversationContext,
        SmallTalkResponse? smallTalkResponse, string customerMessage, bool requiresProductGrounding,
        IReadOnlyCollection<GroundedProduct> products, bool allowPolicyFacts,
        bool allowInventoryFacts, bool allowOrderFacts)
        => new ResponseValidationContext
        {
            Response = response,
            ToneProfile = toneProfile ?? new ToneProfile(),
            ConversationContext = conversationContext ?? new ConversationContext(),
            SmallTalkResponse = smallTalkResponse,
            RequiresFactGrounding = requiresProductGrounding || products.Count > 0,
            AllowedProductNames = products.Select(p => p.Name).ToList(),
            AllowedProductCodes = products.Select(p => p.Code).ToList(),
            AllowedPrices = products.Where(p => p.Price.HasValue).Select(p => p.Price!.Value).ToList(),
            AllowPolicyFacts = allowPolicyFacts,
            AllowInventoryFacts = allowInventoryFacts,
            AllowOrderFacts = allowOrderFacts
        };

    public string FormatAllowedProductNames(IReadOnlyCollection<GroundedProduct> products)
        => products.Count == 0
            ? "khong co"
            : string.Join(", ", products.Select(p => $"{p.Name} ({p.Code})"));

    public string BuildPolicyGiftMessage(StateContext ctx)
    {
        var giftName = ctx.GetData<string>("selectedGiftName");
        return string.IsNullOrWhiteSpace(giftName)
            ? "Hiện tại đơn này chưa có quà tặng theo chính sách đang áp dụng ạ."
            : $"Nếu chốt đơn lúc này thì quà tặng theo chính sách hiện tại là {giftName} ạ.";
    }

    public string BuildPendingContactClarificationReply(StateContext ctx)
    {
        var phone = ctx.GetData<string>("customerPhone");
        var address = ctx.GetData<string>("shippingAddress");
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        var hasAddress = !string.IsNullOrWhiteSpace(address);

        if (hasPhone && hasAddress)
            return $"Dạ em đang giữ SĐT {phone} và địa chỉ {address} từ lần trước của chị ạ. Chị giúp em xác nhận là vẫn dùng đúng 2 thông tin này, hoặc gửi thông tin mới để em cập nhật cho đơn lần này nha.";
        if (hasPhone)
            return $"Dạ em đang giữ SĐT {phone} từ lần trước của chị ạ. Chị giúp em xác nhận số này còn dùng đúng không, rồi gửi em thêm địa chỉ giao hàng hiện tại để em chốt đơn cho mình nha.";
        if (hasAddress)
            return $"Dạ em đang giữ địa chỉ {address} từ lần trước của chị ạ. Chị giúp em xác nhận địa chỉ này còn dùng đúng không, rồi gửi em thêm số điện thoại để em chốt đơn cho mình nha.";
        return "Dạ chị giúp em gửi lại số điện thoại và địa chỉ giao hàng hiện tại để em chốt đơn cho mình nha.";
    }

    public string BuildProductGroundingFallbackReply()
        => ProductGroundingService.FallbackReply;

    public string NormalizeSentence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "sản phẩm chăm sóc da được nhiều chị quan tâm ạ.";
        var normalized = text.Trim().TrimEnd('.', '!', '?');
        return normalized.EndsWith("ạ", StringComparison.OrdinalIgnoreCase)
            ? normalized + "."
            : normalized + " ạ.";
    }

    public List<string> GetMissingContactInfo(StateContext ctx)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone"))) missing.Add("so dien thoai");
        if (string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress"))) missing.Add("dia chi");
        return missing;
    }

    public string BuildDraftConfirmation(StateContext ctx, DraftOrder draftOrder)
    {
        var itemSummary = draftOrder.Items.Count == 0
            ? "đơn của chị"
            : string.Join(", ", draftOrder.Items.Select(i =>
                i.Quantity > 1 ? $"{i.ProductName} x{i.Quantity}" : i.ProductName));
        var giftName = ctx.GetData<string>("selectedGiftName");
        var lines = new List<string>
        {
            $"Dạ em đã lên đơn nháp {draftOrder.DraftCode} cho {itemSummary} rồi ạ.",
            $"Tạm tính tiền hàng hiện tại là {draftOrder.MerchandiseTotal:N0}đ. Phí ship và tổng đơn cuối em sẽ kiểm tra lại theo đơn cụ thể trước khi chốt giao hàng cho chị nha."
        };
        if (!string.IsNullOrWhiteSpace(giftName))
            lines.Add($"Quà tặng đang gắn theo dữ liệu nội bộ hiện tại cho đơn này là {giftName} ạ.");
        lines.Add("Bên em sẽ có bạn kiểm tra lại thông tin và chốt giao hàng cho mình nha.");
        if (ctx.GetData<bool?>("currentOrderUsesUpdatedContact") == true &&
            string.Equals(ctx.GetData<string>("pendingContactQuestion"), "ask_save_new_contact", StringComparison.OrdinalIgnoreCase))
            lines.Add("Nếu chị thấy thông tin mới này đúng rồi thì chị có muốn em cập nhật luôn cho các đơn sau không ạ?");
        return string.Join(" ", lines);
    }

    public string GetContactSummary(StateContext ctx)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(ctx.GetData<string>("customerPhone"));
        var hasAddress = !string.IsNullOrWhiteSpace(ctx.GetData<string>("shippingAddress"));
        var needsConfirmation = ctx.GetData<bool?>("contactNeedsConfirmation") == true;
        return $"SDT={(hasPhone ? (needsConfirmation ? "dang nho lai" : "da co") : "chua co")}, Dia chi={(hasAddress ? (needsConfirmation ? "dang nho lai" : "da co") : "chua co")}";
    }

    public ConversationState DetermineNextState(CustomerIntent intent, bool hasProduct, bool hasContact)
        => intent switch
        {
            CustomerIntent.Browsing => ConversationState.Consulting,
            CustomerIntent.Consulting => ConversationState.Consulting,
            CustomerIntent.ReadyToBuy => hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting,
            CustomerIntent.Confirming => ConversationState.CollectingInfo,
            CustomerIntent.Questioning => ConversationState.Consulting,
            _ => hasProduct ? ConversationState.CollectingInfo : ConversationState.Consulting
        };
}
