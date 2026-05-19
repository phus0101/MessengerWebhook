# Order Management & Conversation Flow Research Report

**Date**: 2026-03-20
**Focus**: Clothing store order management via Facebook Messenger webhook
**Current System**: ASP.NET Core webhook with basic echo functionality

---

## Executive Summary

Research covers state management, product catalog design, order workflows, and conversation patterns for clothing e-commerce chatbot. Current system handles basic message/postback processing with idempotency. Needs conversation state tracking, product catalog integration, multi-step order flow, and session management.

---

## 1. Conversation State Management

### Current Implementation
- **File**: `D:\Projects\Facebook Messgener Webhook Demo\MessengerWebhook\src\MessengerWebhook\Services\WebhookProcessor.cs`
- Simple echo bot with idempotency via MemoryCache (48h TTL)
- No state tracking between messages
- No context preservation across conversations

### Recommended Architecture

**Database-Backed State Store**
- Store per-user conversation state in database (SQL Server/PostgreSQL recommended)
- Track: current_state, context_data, last_interaction, session_id
- State identifiers: GREETING → BROWSING → SELECTING → CONFIRMING → ORDERING → COMPLETED

**State Machine Pattern**
```
States: idle, greeting, product_browsing, size_selection, color_selection,
        cart_review, address_input, payment_pending, order_confirmed

Transitions: User input triggers state changes with validation
Context: Preserve selected products, sizes, colors, quantities across states
```

**Implementation Options**
1. **Simple approach**: String-based state field in User/Session table
2. **Scalable approach**: DynamoDB/Redis for high-volume (millions of conversations)
3. **Event-driven**: State machine with event sourcing for audit trail

**Sources**: [StackOverflow state tracking](https://stackoverflow.com/questions/39075492/what-is-the-right-way-to-save-track-state-inside-a-facebook-messenger-bot), [AI Chatbot Architecture](https://milaan.is-a.dev/blog/ai-chatbot-architecture-bedrock/)

---

## 2. Product Catalog Data Model

### Recommended Schema

**Core Tables**

```sql
-- Products (base items)
Products
  - ProductId (PK)
  - Name
  - Description
  - Category (shirts, pants, dresses, shoes, accessories)
  - BasePrice
  - ImageUrl
  - IsActive
  - CreatedAt

-- Product Variants (color-size combinations)
ProductVariants
  - VariantId (PK)
  - ProductId (FK)
  - ColorId (FK)
  - SizeId (FK)
  - SKU (unique)
  - StockQuantity
  - PriceAdjustment (if variant pricing differs)
  - IsAvailable

-- Colors (reference table)
Colors
  - ColorId (PK)
  - Name (Red, Blue, Black, etc.)
  - HexCode (#FF0000)

-- Sizes (reference table)
Sizes
  - SizeId (PK)
  - Category (mens, womens, kids)
  - SizeCode (XS, S, M, L, XL, 2XL)
  - SortOrder

-- Product Images
ProductImages
  - ImageId (PK)
  - ProductId (FK)
  - VariantId (FK, nullable)
  - ImageUrl
  - IsPrimary
```

**Key Design Decisions**
- Separate variants table handles all color-size-stock combinations
- Not all products have all color-size combos (query by availability)
- Category-specific sizing (men's L ≠ women's L)
- Stock tracking at variant level, not product level

**Sources**: [Database schema for clothes](https://stackoverflow.com/questions/2181223/database-schema-design-about-clothes), [Product color/size storage](https://dba.stackexchange.com/questions/276742/what-is-the-best-way-to-store-product-size-and-color-for-an-ecommerce-database)

---

## 3. Order Creation Workflow

### Multi-Step Conversation Flow

**Phase 1: Greeting & Intent Detection**
- User: "Hi" / "I want to buy clothes"
- Bot: Welcome message + quick replies (Browse Products / Track Order / Help)
- State: GREETING → BROWSING

**Phase 2: Product Discovery**
- Show categories (Shirts, Pants, Dresses, Shoes)
- Display products with images via Generic Template
- User selects product
- State: BROWSING → PRODUCT_SELECTED

**Phase 3: Variant Selection**
- Show available colors for selected product
- Show available sizes for selected color
- Validate stock availability
- State: PRODUCT_SELECTED → SIZE_SELECTION → COLOR_SELECTION

**Phase 4: Cart Management**
- Add to cart (store in session state)
- Show cart summary
- Options: Continue shopping / Proceed to checkout
- State: COLOR_SELECTION → CART_REVIEW

**Phase 5: Order Details**
- Collect shipping address (can use Messenger user profile API)
- Confirm phone number
- Show order summary with total
- State: CART_REVIEW → ADDRESS_INPUT → ORDER_REVIEW

**Phase 6: Order Confirmation**
- Create draft order in database
- Generate order ID
- Send confirmation message
- State: ORDER_REVIEW → ORDER_CONFIRMED

**Phase 7: Payment Integration**
- Option 1: Messenger Payments (if available in region)
- Option 2: External payment link (Stripe, PayPal)
- Option 3: COD (Cash on Delivery)
- Update order status after payment

**Validation Rules**
- Check stock before adding to cart
- Validate address format
- Verify phone number format
- Prevent duplicate orders (idempotency)
- Timeout abandoned carts (30-60 minutes)

**Sources**: [WhatsApp commerce flow](https://koder.ai/blog/whatsapp-commerce-d2c-flow), [Shopify draft orders](https://www.eesel.ai/blog/shopify-chatbot-to-create-draft-order-for-exchange-requests), [Agentic Commerce Protocol](https://developers.openai.com/commerce/)

---

## 4. Conversation Flow Patterns

### Priority Flows for E-commerce

**1. Order Tracking (WISMO - Where Is My Order)**
- Highest volume: 30-40% of support queries
- Flow: User provides order ID → Bot retrieves status → Display tracking info
- Requires: Order database integration, shipping API integration

**2. Product Inquiry**
- User asks about specific product
- Bot searches catalog, shows details, suggests similar items
- Requires: Product search, NLP for intent detection

**3. Order Placement**
- Full multi-step flow (described in section 3)
- Requires: State management, inventory checks, payment integration

**4. Returns/Exchanges**
- User requests return → Verify order → Create return draft → Provide instructions
- Requires: Order history, return policy rules

**Intent-Based Routing**
- Use NLP to detect intent from first message
- Route to appropriate flow: ORDER_TRACKING / PRODUCT_SEARCH / NEW_ORDER / SUPPORT
- Fallback to human agent for complex queries

**Sources**: [E-commerce chatbot priority](https://www.lorikeetcx.ai/articles/ai-chatbot-for-ecommerce), [Intent routing](https://client.botika.online/docs/platform-gpt/example/project/e-commerce.html)

---

## 5. Session Management

### Timeout Strategy

**Recommended Timeouts**
- **Inactivity timeout**: 15 minutes (user stops responding)
- **Session timeout**: 60 minutes (absolute max for order flow)
- **Cart timeout**: 30 minutes (release reserved stock)
- **Payment timeout**: 10 minutes (payment link expiration)

**Timeout Handling**
1. **Warning notification**: Send message at 12 minutes of inactivity
   - "Are you still there? Reply to continue shopping."
2. **Confirmation prompt**: Quick reply buttons (Yes, Continue / No, Cancel)
3. **Graceful cleanup**: Save cart state, release stock, clear session
4. **Resume capability**: Allow user to resume within 24 hours with saved cart

**Session Persistence**
- Store session data in database with expiration timestamp
- Use Redis for fast session lookup (optional)
- Link sessions to Facebook PSID (Page-Scoped ID)
- Handle browser refresh, device switch scenarios

**Security Considerations**
- Timeout prevents session hijacking
- Clear sensitive data (payment info) immediately after use
- Don't store credit card details (PCI compliance)
- Log session activity for fraud detection

**Sources**: [Session timeout best practices](https://quidget.ai/blog/ai-automation/chatbot-session-timeout-settings-best-practices/), [Session lifecycle management](https://agentfactory.panaversity.org/docs/Building-Custom-Agents/chatkit-server/session-lifecycle-management), [Session persistence strategies](https://predictabledialogs.com/learn/ai-stack/session-persistence-ai-chat-continuity-strategies)

---

## 6. Integration with Existing System

### Current Architecture Analysis

**Existing Components**
- `Program.cs`: Webhook endpoints, channel-based async processing
- `WebhookProcessor.cs`: Message/postback handling with idempotency
- `MessengerService.cs`: Facebook Graph API v21.0 integration
- `BackgroundServices/WebhookProcessingService.cs`: Background queue processing
- Channel-based architecture: 1000 event capacity, drops oldest on full

**Required Additions**

**1. Database Layer**
```
Models/
  - ConversationState.cs
  - Product.cs
  - ProductVariant.cs
  - Order.cs
  - OrderItem.cs
  - Cart.cs
  - CartItem.cs

Services/
  - IStateManager.cs / StateManager.cs
  - IProductService.cs / ProductService.cs
  - IOrderService.cs / OrderService.cs
  - ICartService.cs / CartService.cs

DbContext/
  - MessengerBotDbContext.cs (Entity Framework Core)
```

**2. Enhanced WebhookProcessor**
- Replace simple echo logic with state machine
- Load user state from database
- Route to appropriate handler based on state
- Update state after processing
- Handle timeouts and cleanup

**3. Message Templates**
- Generic Template for product listings
- Button Template for quick replies
- Receipt Template for order confirmation
- List Template for cart display

**4. External Integrations**
- Payment gateway (Stripe/PayPal webhook)
- Shipping provider API (tracking updates)
- Inventory management system (stock sync)

---

## 7. Database Schema Recommendations

### Complete Schema

```sql
-- User Sessions
ConversationSessions
  - SessionId (PK, GUID)
  - FacebookPSID (indexed)
  - CurrentState (varchar)
  - StateData (JSON/JSONB)
  - LastInteractionAt
  - ExpiresAt
  - CreatedAt

-- Orders
Orders
  - OrderId (PK)
  - SessionId (FK)
  - FacebookPSID
  - Status (draft, pending_payment, paid, shipped, delivered, cancelled)
  - TotalAmount
  - ShippingAddress (JSON)
  - PhoneNumber
  - PaymentMethod
  - PaymentStatus
  - CreatedAt
  - UpdatedAt

-- Order Items
OrderItems
  - OrderItemId (PK)
  - OrderId (FK)
  - VariantId (FK)
  - Quantity
  - UnitPrice
  - Subtotal

-- Shopping Carts (temporary)
Carts
  - CartId (PK)
  - SessionId (FK)
  - FacebookPSID
  - ExpiresAt
  - CreatedAt

-- Cart Items
CartItems
  - CartItemId (PK)
  - CartId (FK)
  - VariantId (FK)
  - Quantity
  - AddedAt
```

**Indexes**
- ConversationSessions: FacebookPSID, ExpiresAt
- Orders: FacebookPSID, Status, CreatedAt
- Products: Category, IsActive
- ProductVariants: ProductId, StockQuantity, IsAvailable

---

## 8. Implementation Recommendations

### Phase 1: Foundation (Week 1-2)
- Set up database with core tables
- Implement StateManager service
- Create basic state machine (5-6 states)
- Add product catalog CRUD

### Phase 2: Product Flow (Week 3-4)
- Build product browsing flow
- Implement variant selection
- Add cart management
- Create message templates

### Phase 3: Order Flow (Week 5-6)
- Implement order creation
- Add address collection
- Build order confirmation
- Integrate payment gateway

### Phase 4: Advanced Features (Week 7-8)
- Add order tracking
- Implement session timeout handling
- Build admin dashboard
- Add analytics and monitoring

### Testing Strategy
- Unit tests for state transitions
- Integration tests for order flow
- Load testing for concurrent users
- Manual testing with real Messenger accounts

---

## 9. Key Considerations

### Performance
- Use Redis for session caching (optional but recommended for scale)
- Index database properly for fast lookups
- Implement connection pooling for database
- Monitor Graph API rate limits (avoid 429 errors)

### Security
- Validate all user inputs
- Sanitize data before database insertion
- Use parameterized queries (prevent SQL injection)
- Implement CSRF protection for payment callbacks
- Log all transactions for audit trail

### User Experience
- Keep messages concise (mobile-first)
- Use quick replies for common actions
- Show product images (Generic Template)
- Provide clear error messages
- Allow easy cart modification
- Enable conversation restart anytime

### Scalability
- Current channel capacity: 1000 events
- Consider increasing for high traffic
- Use database connection pooling
- Implement caching layer (Redis)
- Monitor queue depth via /metrics endpoint

---

## 10. Conversation Flow Diagram

```
[User Sends Message]
        ↓
[Load Session State from DB]
        ↓
[Determine Current State]
        ↓
    ┌───┴───┐
    │ State │
    └───┬───┘
        ├─→ IDLE: Send greeting, show menu
        ├─→ BROWSING: Show products by category
        ├─→ PRODUCT_VIEW: Show variants (colors/sizes)
        ├─→ SIZE_SELECT: Validate stock, add to cart
        ├─→ CART_REVIEW: Show cart, checkout option
        ├─→ ADDRESS_INPUT: Collect shipping details
        ├─→ ORDER_CONFIRM: Create order, send receipt
        └─→ TRACKING: Show order status
        ↓
[Process User Input]
        ↓
[Update State & Context]
        ↓
[Save to Database]
        ↓
[Send Response via Graph API]
        ↓
[Check Timeout & Cleanup]
```

---

## Unresolved Questions

1. **Payment Integration**: Which payment gateway preferred? (Stripe, PayPal, local provider?)
2. **Inventory Sync**: Real-time or batch sync with existing inventory system?
3. **Multi-language**: Support Vietnamese only or add English/other languages?
4. **Human Handoff**: When to escalate to human agent? Integration with existing support system?
5. **Order Limits**: Max items per order? Max order value?
6. **Shipping**: Flat rate or calculated by location? Integration with shipping providers?
7. **Returns**: Automated return flow or manual approval required?
8. **Analytics**: What metrics to track? (conversion rate, cart abandonment, popular products?)

---

## Sources

- [State tracking best practices](https://stackoverflow.com/questions/39075492/what-is-the-right-way-to-save-track-state-inside-a-facebook-messenger-bot)
- [AI Chatbot Architecture with AWS](https://milaan.is-a.dev/blog/ai-chatbot-architecture-bedrock/)
- [E-commerce chatbot priority flows](https://www.lorikeetcx.ai/articles/ai-chatbot-for-ecommerce)
- [Product catalog integration](https://www.edesk.com/blog/ai-chatbot-ecommerce-product-order-shipping/)
- [Database schema for clothing](https://stackoverflow.com/questions/2181223/database-schema-design-about-clothes)
- [Product variant storage](https://dba.stackexchange.com/questions/276742/what-is-the-best-way-to-store-product-size-and-color-for-an-ecommerce-database)
- [WhatsApp commerce flow](https://koder.ai/blog/whatsapp-commerce-d2c-flow)
- [Shopify draft orders](https://www.eesel.ai/blog/shopify-chatbot-to-create-draft-order-for-exchange-requests)
- [Agentic Commerce Protocol](https://developers.openai.com/commerce/)
- [State machine architecture](https://www.zedhaque.com/blog/voice-agents-state-machines/)
- [Event-driven agents](https://activewizards.com/blog/architecting-event-driven-conversational-agents-with-langgraph)
- [Session timeout best practices](https://quidget.ai/blog/ai-automation/chatbot-session-timeout-settings-best-practices/)
- [Session lifecycle management](https://agentfactory.panaversity.org/docs/Building-Custom-Agents/chatkit-server/session-lifecycle-management)
- [Session persistence strategies](https://predictabledialogs.com/learn/ai-stack/session-persistence-ai-chat-continuity-strategies)
