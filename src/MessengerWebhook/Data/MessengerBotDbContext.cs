using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MessengerWebhook.Services.Tenants;
using Pgvector;

namespace MessengerWebhook.Data;

public class MessengerBotDbContext : DbContext
{
    private readonly ITenantContext? _tenantContext;

    public Guid? CurrentTenantId => _tenantContext?.IsResolved == true ? _tenantContext.TenantId : null;
    public bool IsTenantResolved => CurrentTenantId.HasValue;

    public MessengerBotDbContext(
        DbContextOptions<MessengerBotDbContext> options,
        ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<Color> Colors { get; set; }
    public DbSet<Size> Sizes { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<ConversationSession> ConversationSessions { get; set; }
    public DbSet<ConversationMessage> ConversationMessages { get; set; }
    public DbSet<SkinProfile> SkinProfiles { get; set; }
    public DbSet<IngredientCompatibility> IngredientCompatibilities { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Gift> Gifts { get; set; }
    public DbSet<ProductGiftMapping> ProductGiftMappings { get; set; }
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<FacebookPageConfig> FacebookPageConfigs { get; set; }
    public DbSet<ManagerProfile> ManagerProfiles { get; set; }
    public DbSet<CustomerIdentity> CustomerIdentities { get; set; }
    public DbSet<DraftOrder> DraftOrders { get; set; }
    public DbSet<DraftOrderItem> DraftOrderItems { get; set; }
    public DbSet<RiskSignal> RiskSignals { get; set; }
    public DbSet<VipProfile> VipProfiles { get; set; }
    public DbSet<HumanSupportCase> HumanSupportCases { get; set; }
    public DbSet<BotConversationLock> BotConversationLocks { get; set; }
    public DbSet<KnowledgeSnapshot> KnowledgeSnapshots { get; set; }
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }
    public DbSet<ProductEmbedding> ProductEmbeddings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyTenantFilters(modelBuilder);

        // ConversationSession indexes
        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.FacebookPSID)
            .IsUnique();

        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.LastActivityAt);

        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.FacebookPageId);

        // ProductVariant indexes and constraints
        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => v.SKU)
            .IsUnique();

        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => new { v.ProductId, v.VolumeML, v.Texture })
            .IsUnique();

        // Product indexes
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.TenantId, p.Code })
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Category);

        // Order indexes
        modelBuilder.Entity<Order>()
            .HasIndex(o => o.SessionId);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.Status);

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.CreatedAt);

        // Cart indexes
        modelBuilder.Entity<Cart>()
            .HasIndex(c => c.SessionId);

        modelBuilder.Entity<Cart>()
            .HasIndex(c => c.ExpiresAt);

        // CartItem indexes
        modelBuilder.Entity<CartItem>()
            .HasIndex(i => i.VariantId);

        // OrderItem indexes
        modelBuilder.Entity<OrderItem>()
            .HasIndex(i => i.VariantId);

        // ConversationMessage indexes
        modelBuilder.Entity<ConversationMessage>()
            .HasIndex(m => m.SessionId);

        modelBuilder.Entity<ConversationMessage>()
            .HasIndex(m => m.CreatedAt);

        // SkinProfile indexes
        modelBuilder.Entity<SkinProfile>()
            .HasIndex(s => s.SessionId)
            .IsUnique();

        // IngredientCompatibility indexes
        modelBuilder.Entity<IngredientCompatibility>()
            .HasIndex(i => new { i.Ingredient1, i.Ingredient2 });

        // Gift indexes
        modelBuilder.Entity<Gift>()
            .HasIndex(g => new { g.TenantId, g.Code })
            .IsUnique();

        modelBuilder.Entity<Gift>()
            .HasIndex(g => g.IsActive);

        // ProductGiftMapping indexes
        modelBuilder.Entity<ProductGiftMapping>()
            .HasIndex(m => m.ProductCode);

        modelBuilder.Entity<ProductGiftMapping>()
            .HasIndex(m => m.GiftCode);

        modelBuilder.Entity<ProductGiftMapping>()
            .HasIndex(m => new { m.TenantId, m.ProductCode, m.GiftCode })
            .IsUnique();

        // Multi-page and sales-domain indexes
        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Code)
            .IsUnique();

        modelBuilder.Entity<FacebookPageConfig>()
            .HasIndex(p => p.FacebookPageId)
            .IsUnique();

        modelBuilder.Entity<ManagerProfile>()
            .HasIndex(m => m.Email);

        modelBuilder.Entity<CustomerIdentity>()
            .HasIndex(c => c.FacebookPSID)
            .IsUnique();

        modelBuilder.Entity<CustomerIdentity>()
            .HasIndex(c => c.PhoneNumber);

        modelBuilder.Entity<DraftOrder>()
            .HasIndex(d => d.DraftCode)
            .IsUnique();

        modelBuilder.Entity<DraftOrder>()
            .HasIndex(d => d.Status);

        modelBuilder.Entity<DraftOrder>()
            .HasIndex(d => d.FacebookPageId);

        modelBuilder.Entity<RiskSignal>()
            .HasIndex(r => r.Level);

        modelBuilder.Entity<HumanSupportCase>()
            .HasIndex(c => c.Status);

        modelBuilder.Entity<HumanSupportCase>()
            .HasIndex(c => c.FacebookPSID);

        modelBuilder.Entity<HumanSupportCase>()
            .HasIndex(c => c.FacebookPageId);

        modelBuilder.Entity<BotConversationLock>()
            .HasIndex(c => new { c.FacebookPSID, c.IsLocked });

        modelBuilder.Entity<KnowledgeSnapshot>()
            .HasIndex(k => new { k.Category, k.IsPublished });

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(x => new { x.ResourceType, x.ResourceId });

        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(x => x.CreatedAt);

        // ProductEmbedding indexes
        modelBuilder.Entity<ProductEmbedding>()
            .HasIndex(e => new { e.TenantId, e.ProductId })
            .IsUnique();

        // Relationships
        modelBuilder.Entity<Product>()
            .HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Product>()
            .HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Cart>()
            .HasOne(c => c.Session)
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Cart>()
            .HasMany(c => c.Items)
            .WithOne(i => i.Cart)
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Order>()
            .HasOne(o => o.Session)
            .WithMany()
            .HasForeignKey(o => o.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Order>()
            .HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ConversationMessage>()
            .HasOne(m => m.Session)
            .WithMany()
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SkinProfile>()
            .HasOne(s => s.Session)
            .WithMany()
            .HasForeignKey(s => s.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductGiftMapping>()
            .HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductCode)
            .HasPrincipalKey(p => p.Code)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProductGiftMapping>()
            .HasOne(m => m.Gift)
            .WithMany(g => g.ProductGiftMappings)
            .HasForeignKey(m => m.GiftCode)
            .HasPrincipalKey(g => g.Code)
            .OnDelete(DeleteBehavior.Cascade);

        // Decimal precision
        modelBuilder.Entity<Product>()
            .Property(p => p.BasePrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Product>()
            .Property(p => p.NobitaWeight)
            .HasPrecision(10, 2);

        modelBuilder.Entity<ProductVariant>()
            .Property(v => v.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CartItem>()
            .Property(i => i.Price)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Order>()
            .Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OrderItem>()
            .Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OrderItem>()
            .Property(i => i.TotalPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.FacebookPages)
            .WithOne(p => p.Tenant)
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.Managers)
            .WithOne(m => m.Tenant)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ManagerProfile>()
            .HasOne(m => m.FacebookPageConfig)
            .WithMany()
            .HasForeignKey(m => m.FacebookPageConfigId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CustomerIdentity>()
            .HasOne(c => c.VipProfile)
            .WithOne(v => v.CustomerIdentity)
            .HasForeignKey<VipProfile>(v => v.CustomerIdentityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DraftOrder>()
            .HasOne(d => d.CustomerIdentity)
            .WithMany()
            .HasForeignKey(d => d.CustomerIdentityId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DraftOrder>()
            .HasMany(d => d.Items)
            .WithOne(i => i.DraftOrder)
            .HasForeignKey(i => i.DraftOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RiskSignal>()
            .HasOne(r => r.CustomerIdentity)
            .WithMany(c => c.RiskSignals)
            .HasForeignKey(r => r.CustomerIdentityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RiskSignal>()
            .HasOne(r => r.DraftOrder)
            .WithMany(d => d.RiskSignals)
            .HasForeignKey(r => r.DraftOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DraftOrder>()
            .Property(d => d.MerchandiseTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<DraftOrder>()
            .Property(d => d.ShippingFee)
            .HasPrecision(18, 2);

        modelBuilder.Entity<DraftOrder>()
            .Property(d => d.GrandTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<DraftOrderItem>()
            .Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CustomerIdentity>()
            .Property(c => c.LifetimeValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<VipProfile>()
            .Property(v => v.LifetimeValue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<RiskSignal>()
            .Property(r => r.Score)
            .HasPrecision(5, 2);

        // ProductEmbedding configuration
        modelBuilder.Entity<ProductEmbedding>(entity =>
        {
            entity.HasOne(e => e.Product)
                .WithOne()
                .HasForeignKey<ProductEmbedding>(e => e.ProductId)
                .HasPrincipalKey<Product>(p => p.Id);

            entity.Property(e => e.Embedding)
                .HasColumnType("vector(768)")
                .HasConversion(
                    v => v.ToArray(),
                    v => new Vector(v),
                    new ValueComparer<Vector>(
                        (v1, v2) => v1 != null && v2 != null && v1.ToArray().SequenceEqual(v2.ToArray()),
                        v => v.GetHashCode(),
                        v => new Vector(v.ToArray())
                    )
                );
        });
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<ProductVariant>()
            .HasQueryFilter(x => !IsTenantResolved || x.Product == null || x.Product.TenantId == null || x.Product.TenantId == CurrentTenantId);

        modelBuilder.Entity<ProductImage>()
            .HasQueryFilter(x => !IsTenantResolved || x.Product == null || x.Product.TenantId == null || x.Product.TenantId == CurrentTenantId);

        modelBuilder.Entity<Gift>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<ProductGiftMapping>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<ConversationSession>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<Cart>()
            .HasQueryFilter(x => !IsTenantResolved || x.Session == null || x.Session.TenantId == null || x.Session.TenantId == CurrentTenantId);

        modelBuilder.Entity<CartItem>()
            .HasQueryFilter(x => !IsTenantResolved || x.Cart == null || x.Cart.Session == null || x.Cart.Session.TenantId == null || x.Cart.Session.TenantId == CurrentTenantId);

        modelBuilder.Entity<Order>()
            .HasQueryFilter(x => !IsTenantResolved || x.Session == null || x.Session.TenantId == null || x.Session.TenantId == CurrentTenantId);

        modelBuilder.Entity<OrderItem>()
            .HasQueryFilter(x => !IsTenantResolved || x.Order == null || x.Order.Session == null || x.Order.Session.TenantId == null || x.Order.Session.TenantId == CurrentTenantId);

        modelBuilder.Entity<ConversationMessage>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<SkinProfile>()
            .HasQueryFilter(x => !IsTenantResolved || x.Session == null || x.Session.TenantId == null || x.Session.TenantId == CurrentTenantId);

        modelBuilder.Entity<FacebookPageConfig>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<ManagerProfile>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<CustomerIdentity>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<DraftOrder>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<DraftOrderItem>()
            .HasQueryFilter(x => !IsTenantResolved || x.DraftOrder == null || x.DraftOrder.TenantId == null || x.DraftOrder.TenantId == CurrentTenantId);

        modelBuilder.Entity<RiskSignal>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<VipProfile>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<HumanSupportCase>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<BotConversationLock>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<KnowledgeSnapshot>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<AdminAuditLog>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);

        modelBuilder.Entity<ProductEmbedding>()
            .HasQueryFilter(x => !IsTenantResolved || x.TenantId == null || x.TenantId == CurrentTenantId);
    }
}
