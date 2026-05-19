# Plan: Cải thiện độ tự nhiên câu trả lời bot

## Vấn đề hiện tại

Câu trả lời bot sau khi khách chọn sản phẩm từ related suggestions:

```
Dạ bên em có Mặt Nạ Ngủ Dưỡng Ẩm, giá hiện theo dữ liệu nội bộ là 320,000đ ạ.
Em chưa dám chốt freeship hay phí ship ngay lúc này, để em kiểm tra lại theo đơn cụ thể rồi báo chị chính xác ạ.
Quà tặng đang gắn theo dữ liệu nội bộ hiện tại là Serum dưỡng da sample 5ml ạ, còn ưu đãi khác em cần kiểm tra lại lúc chốt đơn.
Nếu chị muốn em nói kỹ hơn về công dụng hoặc cách dùng thì em tư vấn tiếp ạ.
```

**Điểm cần cải thiện:**
1. Lặp từ "dữ liệu nội bộ" 2 lần → nghe máy móc
2. "Em chưa dám chốt" → hơi cứng nhắc
3. Câu dài, cấu trúc phức tạp

## Mục tiêu

Câu trả lời tự nhiên hơn, mềm mại hơn, giữ đúng thông tin nhưng bớt từ kỹ thuật.

**Câu trả lời mong muốn:**
```
Dạ bên em có Mặt Nạ Ngủ Dưỡng Ẩm, giá 320,000đ ạ.

Về phí ship, em cần kiểm tra lại theo địa chỉ cụ thể của chị để báo chính xác nha.

Quà tặng kèm theo là Serum dưỡng da sample 5ml ạ. Nếu có ưu đãi khác, em sẽ cập nhật thêm khi chốt đơn.

Chị muốn em tư vấn thêm về công dụng hay cách dùng không ạ?
```

## Phases

### Phase 1: Phân tích code hiện tại
- Đọc `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- Tìm method build offer response trong `SalesStateHandlerBase.cs`
- Xác định nơi tạo câu trả lời cho flow: chọn related suggestion → offer

### Phase 2: Sửa system prompt
- Thêm quy tắc tránh lặp từ kỹ thuật
- Hướng dẫn dùng ngôn ngữ tự nhiên, mềm mại
- Yêu cầu tách câu ngắn, rõ ràng

### Phase 3: Sửa code build response (nếu cần)
- Tách logic build thành các câu độc lập
- Thêm line break giữa các phần thông tin
- Loại bỏ/giảm từ "dữ liệu nội bộ"

### Phase 4: Test và verify
- Chạy lại flow: mơ hồ → gợi ý → chọn số
- Kiểm tra câu trả lời có tự nhiên hơn không
- So sánh trước/sau

## Success Criteria

- ✅ Không lặp từ "dữ liệu nội bộ"
- ✅ Câu ngắn, tách rõ từng thông tin
- ✅ Giọng điệu mềm mại, tự nhiên hơn
- ✅ Giữ đúng thông tin: giá, ship, quà tặng, CTA
- ✅ Không hallucinate thông tin

## Files liên quan

- `src/MessengerWebhook/Prompts/sales-closer-system-prompt.txt`
- `src/MessengerWebhook/StateMachine/Handlers/SalesStateHandlerBase.cs`
- Có thể: `src/MessengerWebhook/StateMachine/Handlers/BrowsingProductsStateHandler.cs`
