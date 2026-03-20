using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data;

public class MessengerBotDbContext : DbContext
{
    public MessengerBotDbContext(DbContextOptions<MessengerBotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<Color> Colors { get; set; }
    public DbSet<Size> Sizes { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<ConversationSession> ConversationSessions { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ConversationSession indexes
        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.FacebookPSID)
            .IsUnique();

        modelBuilder.Entity<ConversationSession>()
            .HasIndex(s => s.LastActivityAt);

        // ProductVariant indexes and constraints
        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => v.SKU)
            .IsUnique();

        modelBuilder.Entity<ProductVariant>()
            .HasIndex(v => new { v.ProductId, v.ColorId, v.SizeId })
            .IsUnique();

        // Product indexes
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

        // Decimal precision
        modelBuilder.Entity<Product>()
            .Property(p => p.BasePrice)
            .HasPrecision(18, 2);

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
    }
}
