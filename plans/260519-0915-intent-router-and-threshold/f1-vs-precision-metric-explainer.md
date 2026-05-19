# F1 vs Precision — Chọn metric nào để tune threshold?

> Tài liệu giải thích quyết định tuning metric của Phase 03 (Adaptive Threshold).
> Audience: dev/PM cần hiểu vì sao skincare bot ưu tiên Precision over F1.

---

## 1. Khái niệm cơ bản

Khi bot search sản phẩm cho query "kem dưỡng da khô", giả sử:
- Trong shop có **5 sản phẩm thực sự phù hợp** (ground truth)
- Bot trả về **7 sản phẩm** (gợi ý)
- Trong 7 sản phẩm đó: **4 đúng + 3 sai**

```
┌─────────────────────────────────────────────────────┐
│  Tất cả SP trong shop                                │
│                                                      │
│   ┌──────────────────┐                              │
│   │ Bot gợi ý (7 SP) │                              │
│   │                  │                              │
│   │   ┌────────┐     │      ┌──────────────────┐   │
│   │   │ ĐÚNG   │     │      │ SP phù hợp thực  │   │
│   │   │  (4)   │     │      │ tế (5)           │   │
│   │   │        │─────┼──────┤                  │   │
│   │   └────────┘     │      │  (1 SP đúng nh   │   │
│   │                  │      │   bot bỏ sót)    │   │
│   │   ┌────────┐     │      └──────────────────┘   │
│   │   │ SAI(3) │     │                              │
│   │   └────────┘     │                              │
│   └──────────────────┘                              │
└─────────────────────────────────────────────────────┘
```

---

## 2. Định nghĩa metric

### Precision (Độ chính xác)
> "Trong những SP bot gợi ý, **bao nhiêu phần là đúng**?"

```
Precision = SP đúng được gợi ý / Tổng SP bot gợi ý
          = 4 / 7
          = 0.57 (57%)
```
→ **Cao precision = ít nhiễu, ít gợi ý sai.** Bot "im miệng" khi không chắc chắn.

### Recall (Độ phủ)
> "Trong những SP thực sự phù hợp, **bot tìm được bao nhiêu**?"

```
Recall = SP đúng được gợi ý / Tổng SP phù hợp thực tế
       = 4 / 5
       = 0.80 (80%)
```
→ **Cao recall = ít bỏ sót.** Bot "nói nhiều" để không miss case nào.

### F1 (Trung bình điều hoà)
> "Cân bằng giữa precision và recall."

```
F1 = 2 × (P × R) / (P + R)
   = 2 × (0.57 × 0.80) / (0.57 + 0.80)
   = 0.67
```
→ Phạt nặng nếu 1 trong 2 quá thấp. Đảm bảo **cả 2 đều tốt**.

---

## 3. Tradeoff: Tăng/giảm threshold ảnh hưởng thế nào?

| Threshold | Hành vi | Precision | Recall | Hậu quả |
|---|---|---|---|---|
| **0.40** (thấp) | Bot gợi ý nhiều SP có score thấp | ↓ 0.50 | ↑ 0.95 | Khách thấy SP sai → mất tin tưởng |
| **0.55** (trung) | Cân bằng | 0.70 | 0.85 | F1 cao nhất |
| **0.75** (cao) | Bot chỉ gợi ý SP rất chắc chắn | ↑ 0.92 | ↓ 0.60 | Bỏ sót case → câu "không tìm thấy" tăng |

→ **Đây chính là tradeoff cần tune.**

---

## 4. Áp dụng vào shop skincare cụ thể

### Scenario A — Tối ưu F1 (cân bằng)
**Ưu điểm:**
- Bot trả lời được nhiều câu hỏi hơn (recall cao)
- Phù hợp shop **mới khai trương**, cần khách tương tác để học data

**Nhược điểm:**
- Có lúc gợi ý SP không hoàn toàn đúng
- Vd: khách hỏi "kem cho da dầu mụn" → bot gợi ý cả serum (không sai hoàn toàn nhưng lệch)

### Scenario B — Tối ưu Precision (recommended cho skincare) ⭐
**Ưu điểm:**
- **Khách tin tưởng hơn** — mỗi SP gợi ý đều rất khớp
- Skincare là ngành **brand trust matter**: gợi ý sai 1 lần → khách nghi ngờ chuyên môn
- Khi không chắc → bot **hỏi clarifying question** (loại da gì, vấn đề gì) thay vì đoán bừa

**Nhược điểm:**
- Bot hay nói "Em chưa rõ ý ạ, anh/chị cho em biết..." → có thể gây hơi mất kiên nhẫn
- Cần fallback tốt (đã có ở Phase 01 — list categories khi không match)

### Tại sao skincare ưu tiên Precision?

**Lý do thực tế:**

1. **Da mỗi người khác nhau.** Gợi ý retinol cho da nhạy cảm → khách bị kích ứng → review xấu → khủng hoảng truyền thông.
2. **Khách skincare nghiên cứu kỹ.** Họ Google INCI list, đọc review. Gợi ý sai SP = bot lộ là "ngu" → mất uy tín.
3. **AOV cao, mua ít.** Khách skincare chi 500K-2M cho 1 SP. Họ thà chờ tư vấn kỹ hơn là vớ vẩn mua nhầm.
4. **Vùng an toàn:** Khi bot không chắc → **hỏi lại** rẻ hơn nhiều so với **gợi ý sai**.

---

## 5. Đề xuất cụ thể cho dataset tuning

Trong sweep script Phase 03, output sẽ gồm:

```
threshold | precision@5 | recall@5 | F1   | empty_rate
----------+-------------+----------+------+-----------
0.40      | 0.52        | 0.94     | 0.67 | 2%
0.45      | 0.61        | 0.91     | 0.73 | 4%
0.50      | 0.70        | 0.85     | 0.77 | 7%   ← max F1
0.55      | 0.78        | 0.79     | 0.78 | 11%
0.60      | 0.85        | 0.71     | 0.77 | 16%
0.65      | 0.90        | 0.62     | 0.74 | 24%  ← max precision với recall còn chấp nhận
0.70      | 0.94        | 0.48     | 0.64 | 35%
0.75      | 0.97        | 0.32     | 0.48 | 51%  ← bot im quá nhiều
```

**Rule chọn (đã chốt cho project):**
1. **Filter** `precision ≥ 0.85 AND empty_rate ≤ 0.30`
2. **Pick** row có recall cao nhất
3. **Fallback** nếu không có row qualify → relax precision floor xuống 0.80
4. Vẫn không có → flag manual review

→ Theo bảng trên, **threshold = 0.65** là sweet spot cho skincare.

---

## 6. So sánh với industry

| Ngành | Metric ưu tiên | Lý do |
|---|---|---|
| **Skincare / Pharma** | Precision | Sai = tổn hại → rủi ro pháp lý + reputation |
| **Fashion / Apparel** | Recall hoặc F1 | Khách thích duyệt nhiều, sai cũng OK |
| **Grocery / FMCG** | Recall | Khách cần thấy đủ option |
| **Electronics / Cao cấp** | Precision | AOV cao, khách kỹ tính |
| **Search engine (Google)** | F1 nghiêng precision (top 3) | Đỡ click rác nhưng vẫn cần phủ rộng |

**Skincare = Pharma-adjacent** → industry standard là precision-first.

---

## 7. Tóm tắt

| Aspect | F1 | Precision (khuyến nghị) |
|---|---|---|
| Định nghĩa | Cân bằng đúng + đủ | Chỉ đúng mới gợi ý |
| Phù hợp khi | Data còn ít, cần học | Brand trust matter |
| Tone bot | "Hay nói" | "Cẩn trọng, hay hỏi lại" |
| Hậu quả tệ nhất | Gợi ý lệch | Bot im, đẩy lên CS |
| Cho shop mình | ❌ | ✅ — vì skincare + AOV cao |

---

## 8. Quyết định cho Phase 03 (chốt 2026-05-19)

| Coarse intent | Metric | Selection rule |
|---|---|---|
| `product_lookup` | **Precision-first** | `precision ≥ 0.85 AND empty_rate ≤ 0.30` → max recall. Relax to 0.80 nếu không qualify. |
| `category_discovery` | **F1** | `empty_rate ≤ 0.30` → max F1. (Gợi ý sai danh mục nhẹ hơn gợi ý sai SP.) |
| `policy / order / small_talk / greeting` | — | Không tune (không vào RAG). |
