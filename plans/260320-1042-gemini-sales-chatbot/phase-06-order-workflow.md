# Phase 6: Order Workflow

**Priority**: High
**Status**: Pending
**Duration**: 1.5 weeks
**Dependencies**: Phase 3 (State Machine), Phase 4 (Product Catalog), Phase 5 (Conversation Flows)

---

## Context Links

- Research: [Order Management Report](../reports/researcher-260320-1042-order-management.md)
- State Machine: [Phase 3 - State Machine](./phase-03-state-machine.md)
- Product Catalog: [Phase 4 - Product Catalog](./phase-04-product-catalog.md)

---

## Overview

Implement complete order workflow from cart management to order confirmation. Handle address collection, payment integration (Stripe/PayPal/COD), order creation, and receipt generation.

---

## Key Insights

- Cart timeout: 30min (release reserved stock)
- Collect: shipping address, phone number, payment method
- Draft order creation before payment
- Receipt Template for order confirmation
- Support COD (Cash on Delivery) for Vietnamese market
- Idempotency critical for order creation
- Atomic cart-to-order conversion

---

## Requirements

### Functional
- Add/remove items from cart
- Display cart summary with total
- Collect shipping address (use Messenger profile API)
- Validate phone number format
- Support multiple payment methods (COD, Stripe, PayPal)
- Create draft order in database
- Generate order confirmation with receipt
- Send order tracking information
- Handle cart expiration and cleanup

### Non-Functional
- Cart operations <50ms
- Prevent duplicate orders (idempotency)
- Atomic cart-to-order conversion
- Support 1000+ concurrent carts
- Stock reservation during checkout
- PCI compliance (no card storage)

---

## Architecture

### Order Flow
```
Add to Cart → Cart Review → Collect Address → Collect Phone
  → Select Payment → Create Order → Payment Processing → Order Confirmed
```

### Cart Management
```
Cart (session-based)
  ├── CartItems (variant + quantity)
  ├── ExpiresAt (30min from last update)
  └── TotalAmount (calculated)
```

### Payment Integration
```
COD: Direct order confirmation
Stripe: Generate payment link → Webhook confirmation
PayPal: Generate payment link → Webhook confirmation
```

---

## Related Code Files

### To Create
- `src/MessengerWebhook/Services/Cart/ICartService.cs`
- `src/MessengerWebhook/Services/Cart/CartService.cs`
- `src/MessengerWebhook/Services/Cart/Models/CartDto.cs`
- `src/MessengerWebhook/Services/Cart/Models/CartItemDto.cs`
- `src/MessengerWebhook/Services/Orders/IOrderService.cs`
- `src/MessengerWebhook/Services/Orders/OrderService.cs`
- `src/MessengerWebhook/Services/Orders/Models/OrderDto.cs`
- `src/MessengerWebhook/Services/Orders/Models/CreateOrderRequest.cs`
- `src/MessengerWebhook/Services/Payment/IPaymentService.cs`
- `src/MessengerWebhook/Services/Payment/PaymentService.cs`
- `src/MessengerWebhook/Services/Payment/Providers/CodPaymentProvider.cs`
- `src/MessengerWebhook/Services/Payment/Providers/StripePaymentProvider.cs`
- `src/MessengerWebhook/Services/Messenger/ReceiptTemplateBuilder.cs`
- `src/MessengerWebhook/StateMachine/Handlers/CartReviewStateHandler.cs`
- `src/MessengerWebhook/StateMachine/Handlers/AddressInputStateHandler.cs`
- `src/MessengerWebhook/StateMachine/Handlers/OrderReviewStateHandler.cs`
- `src/MessengerWebhook/StateMachine/Handlers/OrderConfirmedStateHandler.cs`
- `src/MessengerWebhook/BackgroundServices/CartCleanupService.cs`
- `src/MessengerWebhook/Controllers/WebhookController.cs` (payment webhooks)

### To Modify
- `src/MessengerWebhook/Program.cs` (register order services)
- `src/MessengerWebhook/appsettings.json` (payment config)

---

## Implementation Steps

### 1. Create Cart DTOs
```csharp
public class CartDto
{
    public string CartId { get; set; } = string.Empty;
    public string FacebookPSID { get; set; } = string.Empty;
    public List<CartItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CartItemDto
{
    public int CartItemId { get; set; }
    public int VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public string SizeCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public string? ImageUrl { get; set; }
}
```

### 2. Implement ICartService
```csharp
public interface ICartService
{
    Task<CartDto> GetOrCreateCartAsync(string facebookPSID);
    Task<bool> AddItemAsync(string cartId, int variantId, int quantity = 1);
    Task<bool> RemoveItemAsync(string cartId, int cartItemId);
    Task<bool> UpdateQuantityAsync(string cartId, int cartItemId, int quantity);
    Task<CartDto?> GetCartAsync(string cartId);
    Task ClearCartAsync(string cartId);
    Task<bool> ValidateCartAsync(string cartId);
    Task CleanupExpiredCartsAsync();
}
```

### 3. Implement CartService
```csharp
public class CartService : ICartService
{
    private readonly ICartRepository _cartRepo;
    private readonly IVariantService _variantService;
    private readonly ILogger<CartService> _logger;
    private readonly TimeSpan _cartExpiration = TimeSpan.FromMinutes(30);

    public async Task<CartDto> GetOrCreateCartAsync(string facebookPSID)
    {
        var cart = await _cartRepo.GetByPSIDAsync(facebookPSID);

        if (cart == null || cart.ExpiresAt < DateTime.UtcNow)
        {
            // Create new cart
            cart = new Cart
            {
                CartId = Guid.NewGuid().ToString(),
                FacebookPSID = facebookPSID,
                ExpiresAt = DateTime.UtcNow.Add(_cartExpiration),
                CreatedAt = DateTime.UtcNow
            };
            await _cartRepo.CreateAsync(cart);
        }

        return MapToDto(cart);
    }

    public async Task<bool> AddItemAsync(string cartId, int variantId, int quantity = 1)
    {
        // Validate stock availability
        if (!await _variantService.IsAvailableAsync(variantId, quantity))
        {
            _logger.LogWarning("Variant {VariantId} not available", variantId);
            return false;
        }

        var cart = await _cartRepo.GetByIdAsync(cartId);
        if (cart == null) return false;

        // Check if item already in cart
        var existingItem = cart.Items.FirstOrDefault(i => i.VariantId == variantId);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            var variant = await _variantService.GetByIdAsync(variantId);
            cart.Items.Add(new CartItem
            {
                VariantId = variantId,
                Quantity = quantity,
                UnitPrice = variant!.Price,
                AddedAt = DateTime.UtcNow
            });
        }

        // Reserve stock
        await _variantService.ReserveStockAsync(variantId, quantity);

        // Update expiration
        cart.ExpiresAt = DateTime.UtcNow.Add(_cartExpiration);

        await _cartRepo.UpdateAsync(cart);
        return true;
    }

    public async Task<bool> ValidateCartAsync(string cartId)
    {
        var cart = await _cartRepo.GetByIdAsync(cartId);
        if (cart == null) return false;

        // Check all items still available
        foreach (var item in cart.Items)
        {
            if (!await _variantService.IsAvailableAsync(item.VariantId, item.Quantity))
            {
                _logger.LogWarning("Cart {CartId} has unavailable items", cartId);
                return false;
            }
        }

        return true;
    }

    public async Task CleanupExpiredCartsAsync()
    {
        var expiredCarts = await _cartRepo.GetExpiredCartsAsync();
        foreach (var cart in expiredCarts)
        {
            // Release reserved stock
            foreach (var item in cart.Items)
            {
                await _variantService.ReleaseStockAsync(item.VariantId, item.Quantity);
            }

            await _cartRepo.DeleteAsync(cart.CartId);
        }
        _logger.LogInformation("Cleaned up {Count} expired carts", expiredCarts.Count);
    }

    private CartDto MapToDto(Cart cart)
    {
        var items = cart.Items.Select(i => new CartItemDto
        {
            CartItemId = i.CartItemId,
            VariantId = i.VariantId,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Subtotal = i.Quantity * i.UnitPrice
        }).ToList();

        return new CartDto
        {
            CartId = cart.CartId,
            FacebookPSID = cart.FacebookPSID,
            Items = items,
            TotalAmount = items.Sum(i => i.Subtotal),
            ExpiresAt = cart.ExpiresAt,
            CreatedAt = cart.CreatedAt
        };
    }
}
```

### 4. Create Order DTOs
```csharp
public class OrderDto
{
    public int OrderId { get; set; }
    public string FacebookPSID { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateOrderRequest
{
    public string CartId { get; set; } = string.Empty;
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public string PhoneNumber { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
}

public class ShippingAddressDto
{
    public string FullName { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}
```

### 5. Implement IOrderService
```csharp
public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request);
    Task<OrderDto?> GetOrderAsync(int orderId);
    Task<List<OrderDto>> GetOrdersByPSIDAsync(string facebookPSID);
    Task<bool> UpdateOrderStatusAsync(int orderId, string status);
    Task<bool> ConfirmPaymentAsync(int orderId, string paymentId);
}
```

### 6. Implement OrderService
```csharp
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly ICartService _cartService;
    private readonly ILogger<OrderService> _logger;

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        // Validate cart
        if (!await _cartService.ValidateCartAsync(request.CartId))
        {
            throw new InvalidOperationException("Cart validation failed");
        }

        var cart = await _cartService.GetCartAsync(request.CartId);
        if (cart == null)
        {
            throw new InvalidOperationException("Cart not found");
        }

        // Create order with transaction
        using var transaction = await _orderRepo.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                FacebookPSID = cart.FacebookPSID,
                Status = "draft",
                TotalAmount = cart.TotalAmount,
                ShippingAddress = JsonSerializer.Serialize(request.ShippingAddress),
                PhoneNumber = request.PhoneNumber,
                PaymentMethod = request.PaymentMethod,
                PaymentStatus = "pending",
                CreatedAt = DateTime.UtcNow
            };

            // Add order items
            foreach (var cartItem in cart.Items)
            {
                order.Items.Add(new OrderItem
                {
                    VariantId = cartItem.VariantId,
                    Quantity = cartItem.Quantity,
                    UnitPrice = cartItem.UnitPrice,
                    Subtotal = cartItem.Subtotal
                });
            }

            await _orderRepo.CreateAsync(order);

            // Clear cart (stock already reserved)
            await _cartService.ClearCartAsync(request.CartId);

            await transaction.CommitAsync();

            _logger.LogInformation("Order {OrderId} created for {PSID}", order.OrderId, order.FacebookPSID);

            return MapToDto(order);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create order");
            throw;
        }
    }

    public async Task<bool> ConfirmPaymentAsync(int orderId, string paymentId)
    {
        var order = await _orderRepo.GetByIdAsync(orderId);
        if (order == null) return false;

        order.PaymentStatus = "paid";
        order.Status = "confirmed";
        order.UpdatedAt = DateTime.UtcNow;

        await _orderRepo.UpdateAsync(order);

        _logger.LogInformation("Payment confirmed for order {OrderId}", orderId);
        return true;
    }
}
```

### 7. Implement Payment Service
```csharp
public interface IPaymentService
{
    Task<string> CreatePaymentLinkAsync(OrderDto order);
    Task<bool> VerifyPaymentAsync(string paymentId);
}

public class PaymentService : IPaymentService
{
    private readonly Dictionary<string, IPaymentProvider> _providers;

    public async Task<string> CreatePaymentLinkAsync(OrderDto order)
    {
        if (order.PaymentMethod == "COD")
        {
            // No payment link needed for COD
            return string.Empty;
        }

        if (!_providers.TryGetValue(order.PaymentMethod, out var provider))
        {
            throw new NotSupportedException($"Payment method {order.PaymentMethod} not supported");
        }

        return await provider.CreatePaymentLinkAsync(order);
    }
}

public interface IPaymentProvider
{
    Task<string> CreatePaymentLinkAsync(OrderDto order);
    Task<bool> VerifyPaymentAsync(string paymentId);
}

public class CodPaymentProvider : IPaymentProvider
{
    public Task<string> CreatePaymentLinkAsync(OrderDto order)
    {
        // COD doesn't need payment link
        return Task.FromResult(string.Empty);
    }

    public Task<bool> VerifyPaymentAsync(string paymentId)
    {
        // COD verified on delivery
        return Task.FromResult(true);
    }
}
```

### 8. Implement Receipt Template Builder
```csharp
public class ReceiptTemplateBuilder
{
    public object BuildReceipt(OrderDto order)
    {
        return new
        {
            attachment = new
            {
                type = "template",
                payload = new
                {
                    template_type = "receipt",
                    recipient_name = order.ShippingAddress.FullName,
                    order_number = order.OrderId.ToString(),
                    currency = "VND",
                    payment_method = order.PaymentMethod,
                    timestamp = new DateTimeOffset(order.CreatedAt).ToUnixTimeSeconds(),
                    elements = order.Items.Select(item => new
                    {
                        title = item.ProductName,
                        subtitle = $"{item.ColorName} - {item.SizeCode}",
                        quantity = item.Quantity,
                        price = item.UnitPrice,
                        currency = "VND"
                    }).ToArray(),
                    address = new
                    {
                        street_1 = order.ShippingAddress.AddressLine1,
                        street_2 = order.ShippingAddress.AddressLine2,
                        city = order.ShippingAddress.City,
                        postal_code = order.ShippingAddress.PostalCode,
                        state = order.ShippingAddress.Province,
                        country = "VN"
                    },
                    summary = new
                    {
                        subtotal = order.TotalAmount,
                        total_cost = order.TotalAmount
                    }
                }
            }
        };
    }
}
```

### 9. Implement State Handlers

**CartReviewStateHandler:**
```csharp
public class CartReviewStateHandler : IStateHandler
{
    private readonly ICartService _cartService;
    private readonly ITemplateBuilder _templateBuilder;
    private readonly IMessengerService _messengerService;
    private readonly IStateMachine _stateMachine;

    public async Task<string> HandleAsync(StateContext context, string message)
    {
        var cartId = context.GetData<string>("cartId");
        if (string.IsNullOrEmpty(cartId))
        {
            return "Giỏ hàng của bạn đang trống. Hãy chọn sản phẩm để thêm vào giỏ!";
        }

        var cart = await _cartService.GetCartAsync(cartId);
        if (cart == null || !cart.Items.Any())
        {
            return "Giỏ hàng của bạn đang trống.";
        }

        // Show cart summary
        var template = _templateBuilder.BuildCartSummary(cart.Items, cart.TotalAmount);
        await _messengerService.SendTemplateAsync(context.FacebookPSID, template);

        // Ask for next action
        var quickReplies = new List<QuickReply>
        {
            new() { Title = "Tiếp tục mua", Payload = "CONTINUE_SHOPPING" },
            new() { Title = "Thanh toán", Payload = "CHECKOUT" }
        };

        await _messengerService.SendQuickRepliesAsync(
            context.FacebookPSID,
            $"Tổng cộng: {cart.TotalAmount:N0} VNĐ\nBạn muốn làm gì tiếp theo?",
            quickReplies);

        return string.Empty;
    }
}
```

**AddressInputStateHandler:**
```csharp
public class AddressInputStateHandler : IStateHandler
{
    private readonly IConversationManager _conversationManager;
    private readonly IStateMachine _stateMachine;

    public async Task<string> HandleAsync(StateContext context, string message)
    {
        // Use AI to extract address information
        var prompt = $@"Trích xuất thông tin địa chỉ từ tin nhắn sau:
{message}

Trả về JSON:
{{
  ""fullName"": ""..."",
  ""addressLine1"": ""..."",
  ""city"": ""..."",
  ""province"": ""..."",
  ""postalCode"": ""...""
}}";

        var response = await _conversationManager.GenerateResponseAsync(
            context,
            prompt,
            new IntentResult { Type = IntentType.Unknown });

        try
        {
            var address = JsonSerializer.Deserialize<ShippingAddressDto>(response);
            context.SetData("shippingAddress", address);

            await _stateMachine.TransitionAsync(context, ConversationState.OrderReview);

            return "Cảm ơn! Vui lòng cung cấp số điện thoại để chúng tôi liên hệ khi giao hàng.";
        }
        catch
        {
            return "Xin lỗi, tôi không hiểu địa chỉ của bạn. Vui lòng nhập lại theo format:\nHọ tên\nĐịa chỉ\nThành phố, Tỉnh";
        }
    }
}
```

**OrderReviewStateHandler:**
```csharp
public class OrderReviewStateHandler : IStateHandler
{
    private readonly IOrderService _orderService;
    private readonly IPaymentService _paymentService;
    private readonly IStateMachine _stateMachine;

    public async Task<string> HandleAsync(StateContext context, string message)
    {
        // Collect phone number
        if (string.IsNullOrEmpty(context.GetData<string>("phoneNumber")))
        {
            var phoneMatch = Regex.Match(message, @"0\d{9}");
            if (!phoneMatch.Success)
            {
                return "Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại 10 chữ số (bắt đầu bằng 0).";
            }

            context.SetData("phoneNumber", phoneMatch.Value);

            // Ask for payment method
            var quickReplies = new List<QuickReply>
            {
                new() { Title = "COD (Tiền mặt)", Payload = "PAYMENT_COD" },
                new() { Title = "Chuyển khoản", Payload = "PAYMENT_BANK" }
            };

            await _messengerService.SendQuickRepliesAsync(
                context.FacebookPSID,
                "Chọn phương thức thanh toán:",
                quickReplies);

            return string.Empty;
        }

        // Create order
        var request = new CreateOrderRequest
        {
            CartId = context.GetData<string>("cartId")!,
            ShippingAddress = context.GetData<ShippingAddressDto>("shippingAddress")!,
            PhoneNumber = context.GetData<string>("phoneNumber")!,
            PaymentMethod = message.Contains("COD") ? "COD" : "BANK"
        };

        var order = await _orderService.CreateOrderAsync(request);
        context.SetData("orderId", order.OrderId);

        await _stateMachine.TransitionAsync(context, ConversationState.OrderConfirmed);

        return $"Đơn hàng #{order.OrderId} đã được tạo thành công!";
    }
}
```

### 10. Implement Cart Cleanup Service
```csharp
public class CartCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cartService = scope.ServiceProvider.GetRequiredService<ICartService>();

                await cartService.CleanupExpiredCartsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cart cleanup");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}
```

### 11. Register Services in Program.cs
```csharp
// Cart services
builder.Services.AddScoped<ICartService, CartService>();

// Order services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Payment providers
builder.Services.AddScoped<IPaymentProvider, CodPaymentProvider>();

// Template builders
builder.Services.AddSingleton<ReceiptTemplateBuilder>();

// Background services
builder.Services.AddHostedService<CartCleanupService>();
```

### 12. Write Unit Tests
```csharp
[Fact]
public async Task AddItemAsync_ValidVariant_AddsToCart()
{
    var result = await _cartService.AddItemAsync(cartId, variantId, 1);
    Assert.True(result);

    var cart = await _cartService.GetCartAsync(cartId);
    Assert.Single(cart!.Items);
}

[Fact]
public async Task CreateOrderAsync_ValidCart_CreatesOrder()
{
    var request = new CreateOrderRequest { /* ... */ };
    var order = await _orderService.CreateOrderAsync(request);

    Assert.NotNull(order);
    Assert.Equal("draft", order.Status);
}

[Fact]
public async Task CleanupExpiredCartsAsync_ReleasesStock()
{
    // Create expired cart
    // Run cleanup
    // Verify stock released
}
```

---

## Todo List

- [ ] Create Cart and Order DTOs
- [ ] Implement ICartService interface
- [ ] Implement CartService with stock reservation
- [ ] Implement IOrderService interface
- [ ] Implement OrderService with transactions
- [ ] Implement IPaymentService interface
- [ ] Implement COD payment provider
- [ ] Implement ReceiptTemplateBuilder
- [ ] Implement CartReviewStateHandler
- [ ] Implement AddressInputStateHandler
- [ ] Implement OrderReviewStateHandler
- [ ] Implement OrderConfirmedStateHandler
- [ ] Implement CartCleanupService
- [ ] Register all services in DI container
- [ ] Write unit tests for cart service
- [ ] Write unit tests for order service
- [ ] Integration test full order flow
- [ ] Test payment webhooks (if using Stripe/PayPal)

---

## Success Criteria

- Cart operations work correctly
- Stock reservation prevents overselling
- Order creation is atomic (no partial orders)
- Cart cleanup releases stock properly
- Receipt template displays correctly
- Address collection works with AI
- Phone validation works
- COD payment flow completes
- Unit tests pass (100% coverage)
- Integration tests pass for full flow

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Stock overselling | Medium | Critical | Use transactions, stock reservation |
| Duplicate orders | Low | High | Implement idempotency keys |
| Cart race conditions | Medium | Medium | Use database locks |
| Payment webhook failures | Medium | High | Implement retry logic, manual reconciliation |

---

## Security Considerations

- Never store credit card details (PCI compliance)
- Validate all user inputs (address, phone)
- Use HTTPS for payment webhooks
- Verify webhook signatures (Stripe/PayPal)
- Log all order transactions for audit
- Rate limit order creation per user

---

## Next Steps

After Phase 6 completion:
1. Proceed to Phase 7: Testing & Optimization
2. Test full order flow end-to-end
3. Implement payment gateway integration (Stripe/PayPal)
4. Set up webhook endpoints for payment confirmation
5. Test with real payment transactions (sandbox mode)
