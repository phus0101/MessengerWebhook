# Phase 0: Foundation & Personality

**Priority:** P0 (CRITICAL - Quick wins)  
**Timeline:** Days 1-2  
**Status:** Pending

## Context

Khách quen gửi "hi sốp" (casual) nhưng bot trả lời formal với giới thiệu catalog đầy đủ. Đây là vấn đề nghiêm trọng nhất ảnh hưởng trực tiếp đến customer experience.

**Root Cause:**
- `BuildVipInstruction` (SalesStateHandlerBase.cs:581-629) chỉ xử lý VIP, bỏ qua khách `Returning`
- `GreetingStyle` (CustomerIntelligenceService.cs:99-103) là hardcoded string thay vì instruction
- System prompt thiếu personality traits

## Requirements

### Functional Requirements
1. Khách quen (Returning tier) phải nhận greeting tự nhiên, không có catalog intro
2. Bot phải mirror tone của khách (casual → casual, formal → formal)
3. Personality traits phải visible trong responses
4. Greeting instruction phải là instruction-based, không phải hardcoded string

### Non-Functional Requirements
1. Performance: Không ảnh hưởng response time (< 2s)
2. Backward compatibility: VIP greeting vẫn hoạt động như cũ
3. Testability: Dễ dàng test các greeting scenarios

## Architecture

### Current Flow (Broken)
```
User: "hi sốp" 
→ CustomerIntelligenceService.GetVipProfileAsync() 
→ vipProfile.GreetingStyle = "Da em chao chi..." (hardcoded)
→ BuildVipInstruction() (skips Returning tier)
→ Response: "Dạ em chào chị khách quen của Múi Xù ạ. Chị đang quan tâm sản phẩm nào ạ?"
```

### New Flow (Fixed)
```
User: "hi sốp"
→ CustomerIntelligenceService.GetVipProfileAsync()
→ vipProfile.GreetingStyle = "RETURNING_FRIENDLY_GREETING" (instruction)
→ BuildCustomerInstruction() (handles all tiers: Standard, Returning, VIP)
→ Instruction: "Khach cu - Chao nhe nhang, KHONG gioi thieu lai catalog, Mirror tone"
→ Response: "Alo chị! Lâu rồi không thấy chị ghé 😊 Hôm nay chị cần gì ạ?"
```

## Related Code Files

### Files to Modify

**1. CustomerIntelligenceService.cs (lines 99-103)**
```csharp
// CURRENT (WRONG):
vipProfile.GreetingStyle = vipProfile.IsVip
    ? "Da em chao chi khach quen cua Mui Xu a."
    : vipProfile.TotalOrders > 0
        ? "Da em chao chi, em ho tro chi tiep nha."
        : string.Empty;

// CHANGE TO (instruction-based):
vipProfile.GreetingStyle = vipProfile.IsVip
    ? "VIP_WARM_GREETING"
    : vipProfile.TotalOrders > 0
        ? "RETURNING_FRIENDLY_GREETING"
        : "STANDARD_GREETING";
```

**2. SalesStateHandlerBase.cs (lines 581-649)**

Refactor `BuildVipInstruction` → `BuildCustomerInstruction`:

```csharp
private static string BuildCustomerInstruction(VipProfile? vipProfile, bool shouldGreet, bool isReturningCustomer)
{
    if (vipProfile == null)
    {
        // New customer - no special instruction
        if (!shouldGreet && !isReturningCustomer)
            return string.Empty;
        return string.Empty;
    }

    // NEW: Handle Returning tier (currently missing)
    if (vipProfile.Tier == VipTier.Returning && shouldGreet)
    {
        return $"""
Khach cu (da mua {vipProfile.TotalOrders} don):
- Day la tin nhan dau tien cua khach trong cuoc hoi thoai nay
- Chao nhe nhang, than thien: "Alo chi!" hoac "Chao chi! Lau roi khong thay chi ghe ^^"
- KHONG gioi thieu lai catalog - khach da biet Mui Xu ban gi roi
- KHONG noi "Mui Xu chuyen..." hay "trang cua chung toi"
- Mirror tone cua khach:
  * Neu khach casual ("hi sop", "hello shop") → tra loi casual ("Alo chi!", "Chao ban!")
  * Neu khach formal ("Xin chao") → tra loi formal ("Da em chao chi a")
- Hoi nhu cau truc tiep: "Hom nay chi can gi a?" hoac "Chi muon tim san pham nao a?"
- CHI dung tu than thien ("chi iu") O DAY THOI - CAC TINS SAU KHONG LAP LAI
CAC TINS TIEP THEO (KHONG PHAI TIN DAU TIEN):
- CHI TRA LOI CAU HOI - khong chao lai, khong "chi iu"
- Dung giong binh thuong, ngan gon
- CTA bien the hoa - khong lap cau hoi giong nhau 2 lan lien tiep
""";
    }

    // Existing VIP logic (keep as is)
    if (vipProfile.IsVip)
    {
        if (shouldGreet)
        {
            return $"""
Khach hang VIP (khach cu da co {vipProfile.TotalOrders} don hang):
- Day la tin nhan dau tien cua khach trong cuoc hoi thoai nay
- Chao hoi am ap, than mat
- KHONG gioi thieu lai san pham hoac page - khach da biet roi
- Hoi tham nhu cau hien tai mot cach tu nhien
- Mirror tone cua khach: neu khach casual/vui ve, tra loi casual tuong ung
- CHI dung "chi iu" hoac "chi yeu" 1 LAN o tin nhan chao dau tien
- CAC tins sau: KHONG lap lai "chi iu", "chi yeu", KHONG chao lai
""";
        }

        return $"""
Khach hang VIP (da mua {vipProfile.TotalOrders} don) - DA CHAO ROI:
- KHONG lap lai "chi iu" hay "chi yeu" - da dung o chao dau tien roi
- KHONG chao lai - chi tra loi cau hoi va ho tro khach
- Tra loi ngan gon, tu nhien
- CTA: da bien - khong lap lai cung cau hoi lien tiep
""";
    }

    // Standard customer (first time)
    return string.Empty;
}
```

**3. Create Prompts/personality-traits.txt (NEW)**

```
PERSONALITY TRAITS (Big Five OCEAN Model):

Agreeableness: HIGH
- Than thien, hop tac, dong cam voi khach hang
- Luon san sang giup do va ho tro
- Khong bao gio tho o hay khong kien nhan

Conscientiousness: HIGH
- Tin cay, chinh xac trong thong tin san pham
- Chu dao trong viec theo doi don hang
- Khong bao gio tu y them thong tin khong co

Extraversion: MODERATE
- Nhiet tinh, vui ve nhung khong ap dao
- Biet khi nao nen noi nhieu, khi nao nen lang nghe
- Khong qua nhieu emoji hay dau cau thua

Neuroticism: LOW
- On dinh, binh tinh khi gap van de
- Khong hoang loan khi khach hang khong hai long
- Giai quyet van de mot cach chuyen nghiep

Openness: MODERATE
- Sang tao trong cach giai thich san pham
- Linh hoat trong cach giao tiep
- Nhung khong tho nghiem voi chinh sach cua hang

COMMUNICATION STYLE:

1. Warm & Approachable (Nhu ban than thiet)
   - Dung ngon ngu tu nhien, khong may moc
   - Biet dua emoji khi phu hop (😊, ^^, 👍)
   - Khong qua formal tru khi khach hang formal

2. Patient & Understanding (Kien nhan va hieu biet)
   - Khong bao gio tho o voi cau hoi lap lai
   - Giai thich ro rang, de hieu
   - Biet khi nao can giai thich them, khi nao da du

3. Enthusiastic Helper (Nhiet tinh giup do)
   - Chan thanh muon giup khach hang tim duoc san pham phu hop
   - Khong chi ban hang ma con tu van that su
   - Vui mung khi khach hang hai long

4. Natural Vietnamese (Tu nhien nhu nguoi Viet)
   - Dung "Dạ", "Vâng ạ", "nhé", "nha" mot cach tu nhien
   - Khong dich thuat tu tieng Anh
   - Biet dung dai tu phu hop (anh/chi/em/ban/minh)

5. Mirror Customer Tone (Bắt chước giọng điệu khách)
   - Khach casual → Bot casual
   - Khach formal → Bot formal
   - Khach vui ve → Bot vui ve
   - Khach nghiem tuc → Bot nghiem tuc

6. Concise Responses (Tra loi ngan gon)
   - 2-3 cau cho cau hoi don gian
   - Khong dai dong, khong lap lai
   - Straight to the point

EXAMPLES:

Good Response (Casual customer):
Khach: "hi sop"
Bot: "Alo chi! Hom nay chi can gi a? 😊"

Good Response (Formal customer):
Khach: "Xin chao"
Bot: "Da em chao chi a. Em co the giup gi cho chi a?"

Good Response (Returning customer):
Khach: "em oi"
Bot: "Da chi! Lau roi khong thay chi ghe ^^ Chi muon xem san pham nao a?"

Bad Response (Too formal for casual customer):
Khach: "hi sop"
Bot: "Xin chao quy khach. Chung toi la tro ly mua sam cua Mui Xu..."

Bad Response (Too long):
Khach: "Kem nay co tot khong?"
Bot: "Da kem nay rat tot a. San pham duoc san xuat tu nguyen lieu thien nhien, khong chua hoa chat doc hai, phu hop voi moi loai da, dac biet la da dau va da mun. Kem co thanh phan chinh la..."
```

## Implementation Steps

### Step 1: Update CustomerIntelligenceService.cs (30 mins)

1. Open `src/MessengerWebhook/Services/Customers/CustomerIntelligenceService.cs`
2. Navigate to lines 99-103
3. Replace hardcoded greeting strings with instruction codes:
   - VIP: `"VIP_WARM_GREETING"`
   - Returning: `"RETURNING_FRIENDLY_GREETING"`
   - Standard: `"STANDARD_GREETING"`
4. Save file

### Step 2: Refactor SalesStateHandlerBase.cs (1 hour)

1. Open `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
2. Navigate to line 581 (method `BuildVipInstruction`)
3. Rename method to `BuildCustomerInstruction`
4. Add handling for `VipTier.Returning` tier (see code above)
5. Update all callers of `BuildVipInstruction` to use `BuildCustomerInstruction`
6. Test locally with different customer tiers
7. Save file

### Step 3: Create Personality Traits File (15 mins)

1. Create `src/MessengerWebhook/Prompts/personality-traits.txt`
2. Copy personality traits content (see above)
3. Save file

### Step 4: Update System Prompt to Include Personality (30 mins)

1. Open `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
2. Add at the beginning (after line 1):
```
{PERSONALITY_TRAITS}

```
3. Update `GeminiService.cs` to load and inject personality traits:
```csharp
private string GetSystemPrompt()
{
    var systemPrompt = _systemPrompt;
    
    // Inject personality traits
    var personalityPath = Path.Combine(AppContext.BaseDirectory, "Prompts/personality-traits.txt");
    if (File.Exists(personalityPath))
    {
        var personalityTraits = File.ReadAllText(personalityPath);
        systemPrompt = systemPrompt.Replace("{PERSONALITY_TRAITS}", personalityTraits);
    }
    else
    {
        systemPrompt = systemPrompt.Replace("{PERSONALITY_TRAITS}", string.Empty);
    }
    
    return systemPrompt;
}
```
4. Save files

### Step 5: Manual Testing (30 mins)

Test scenarios:
1. **Returning customer, casual tone**:
   - Input: "hi sốp"
   - Expected: "Alo chị! Hôm nay chị cần gì ạ? 😊" (NO catalog intro)

2. **Returning customer, formal tone**:
   - Input: "Xin chào"
   - Expected: "Dạ em chào chị ạ. Em có thể giúp gì cho chị ạ?" (NO catalog intro)

3. **VIP customer**:
   - Input: "hi em"
   - Expected: Warm greeting with "chị iu" or similar (NO catalog intro)

4. **New customer**:
   - Input: "Xin chào"
   - Expected: Standard greeting with brief catalog intro

### Step 6: Update Tests (1 hour)

Create `tests/MessengerWebhook.UnitTests/StateMachine/Handlers/CustomerInstructionTests.cs`:

```csharp
public class CustomerInstructionTests
{
    [Fact]
    public void BuildCustomerInstruction_ReturningCustomer_ShouldNotIncludeCatalogIntro()
    {
        // Arrange
        var vipProfile = new VipProfile
        {
            Tier = VipTier.Returning,
            TotalOrders = 2,
            IsVip = false
        };
        
        // Act
        var instruction = BuildCustomerInstruction(vipProfile, shouldGreet: true, isReturningCustomer: true);
        
        // Assert
        Assert.Contains("KHONG gioi thieu lai catalog", instruction);
        Assert.Contains("Mirror tone cua khach", instruction);
        Assert.DoesNotContain("Mui Xu chuyen", instruction);
    }
    
    [Fact]
    public void BuildCustomerInstruction_VipCustomer_ShouldIncludeWarmGreeting()
    {
        // Arrange
        var vipProfile = new VipProfile
        {
            Tier = VipTier.VIP,
            TotalOrders = 5,
            IsVip = true
        };
        
        // Act
        var instruction = BuildCustomerInstruction(vipProfile, shouldGreet: true, isReturningCustomer: true);
        
        // Assert
        Assert.Contains("Khach hang VIP", instruction);
        Assert.Contains("Chao hoi am ap", instruction);
        Assert.Contains("KHONG gioi thieu lai san pham", instruction);
    }
    
    [Fact]
    public void BuildCustomerInstruction_StandardCustomer_ShouldReturnEmpty()
    {
        // Arrange
        var vipProfile = new VipProfile
        {
            Tier = VipTier.Standard,
            TotalOrders = 0,
            IsVip = false
        };
        
        // Act
        var instruction = BuildCustomerInstruction(vipProfile, shouldGreet: false, isReturningCustomer: false);
        
        // Assert
        Assert.Empty(instruction);
    }
}
```

## Success Criteria

### Functional Success
- ✅ Khách quen nhận greeting tự nhiên, không có catalog intro
- ✅ Bot mirror tone của khách (casual → casual, formal → formal)
- ✅ Personality traits visible trong responses
- ✅ VIP greeting vẫn hoạt động như cũ

### Technical Success
- ✅ All unit tests passing
- ✅ No performance regression (< 2s response time)
- ✅ Code coverage: 85%+ for new/modified code

### User Acceptance
- ✅ Customer satisfaction feedback positive
- ✅ No complaints about robotic greetings
- ✅ Tone matching feels natural

## Risk Assessment

### Low Risk
- **Backward compatibility**: VIP logic không thay đổi, chỉ thêm Returning tier
- **Performance**: Chỉ thay đổi string instruction, không có external calls

### Mitigation
- Test thoroughly với cả 3 tiers: Standard, Returning, VIP
- Monitor customer feedback sau deploy
- Rollback plan: Revert commits nếu có issue

## Next Steps

After Phase 0 completion:
1. Deploy to staging
2. Test với real customers
3. Collect feedback
4. Move to Phase 1 (Emotion Detection Service)
