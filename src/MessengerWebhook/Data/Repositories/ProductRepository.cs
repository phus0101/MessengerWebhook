using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using MessengerWebhook.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessengerWebhook.Data.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly MessengerBotDbContext _context;

    public ProductRepository(MessengerBotDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetByCategoryAsync(ProductCategory category)
    {
        return await _context.Products
            .Where(p => p.Category == category && p.IsActive)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(string id)
    {
        return await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product?> GetActiveByIdAsync(string id, Guid tenantId)
    {
        return await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive && (p.TenantId == tenantId || p.TenantId == null));
    }

    public async Task<Product?> GetActiveByCodeAsync(string code, Guid tenantId)
    {
        return await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive && (p.TenantId == tenantId || p.TenantId == null));
    }

    public async Task<List<Product>> GetActiveRelatedAsync(Guid tenantId, ProductCategory? category, IReadOnlyCollection<string> normalizedTerms, int maxCount, CancellationToken cancellationToken = default)
    {
        var boundedMaxCount = Math.Clamp(maxCount, 1, 5);
        var terms = normalizedTerms
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Select(NormalizeSearchText)
            .Distinct()
            .ToList();

        if (category == null && terms.Count == 0)
        {
            return new List<Product>();
        }

        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && (p.TenantId == tenantId || p.TenantId == null));

        if (category.HasValue)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        var primaryTermVariants = terms.Count > 0
            ? BuildSearchTermVariants(terms[0])
            : new List<string>();

        if (primaryTermVariants.Count > 0)
        {
            query = ApplyPrimaryTermFilter(query, primaryTermVariants);
        }

        var products = await query.ToListAsync(cancellationToken);
        if (primaryTermVariants.Count > 0)
        {
            var normalizedPrimaryTermVariants = primaryTermVariants
                .Select(NormalizeSearchText)
                .Distinct()
                .ToList();

            products = products
                .Where(product => normalizedPrimaryTermVariants.Any(term => ContainsNormalizedTerm(product, term)))
                .ToList();
        }

        var rankingTerms = terms.Skip(1).ToList();

        return products
            .OrderByDescending(product => rankingTerms.Count(term => ContainsNormalizedTerm(product, term)))
            .ThenByDescending(product => product.TenantId == tenantId)
            .ThenBy(p => p.Name)
            .ThenBy(p => p.Code)
            .ThenBy(p => p.Id)
            .Take(boundedMaxCount)
            .ToList();
    }

    private static IQueryable<Product> ApplyPrimaryTermFilter(IQueryable<Product> query, IReadOnlyCollection<string> primaryTermVariants)
    {
        Expression<Func<Product, bool>>? predicate = null;
        foreach (var term in primaryTermVariants.Select(term => term.ToLowerInvariant()))
        {
            Expression<Func<Product, bool>> termPredicate = product =>
                product.Name.ToLower().Contains(term) ||
                product.Code.ToLower().Contains(term) ||
                product.Description.ToLower().Contains(term);

            predicate = predicate == null ? termPredicate : Or(predicate, termPredicate);
        }

        return predicate == null ? query : query.Where(predicate);
    }

    private static Expression<Func<Product, bool>> Or(Expression<Func<Product, bool>> left, Expression<Func<Product, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(Product));
        var leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body)!;
        var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body)!;
        return Expression.Lambda<Func<Product, bool>>(Expression.OrElse(leftBody, rightBody), parameter);
    }

    private static List<string> BuildSearchTermVariants(string normalizedTerm)
    {
        var variants = new List<string> { normalizedTerm };
        if (string.Equals(normalizedTerm, "mat na", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add("mặt nạ");
        }
        else if (string.Equals(normalizedTerm, "sua rua mat", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add("sữa rửa mặt");
        }
        else if (string.Equals(normalizedTerm, "chong nang", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add("chống nắng");
        }
        else if (string.Equals(normalizedTerm, "tay trang", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add("tẩy trang");
        }
        else if (string.Equals(normalizedTerm, "nuoc hoa hong", StringComparison.OrdinalIgnoreCase))
        {
            variants.Add("nước hoa hồng");
        }

        return variants.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool ContainsNormalizedTerm(Product product, string normalizedTerm)
    {
        return NormalizeSearchText(product.Name).Contains(normalizedTerm, StringComparison.Ordinal) ||
               NormalizeSearchText(product.Code).Contains(normalizedTerm, StringComparison.Ordinal) ||
               NormalizeSearchText(product.Description).Contains(normalizedTerm, StringComparison.Ordinal);
    }

    private static string NormalizeSearchText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == source ? target : node;
        }
    }

    public async Task<Product?> GetByCodeAsync(string code)
    {
        return await _context.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive);
    }

    public async Task<List<ProductVariant>> GetVariantsByProductIdAsync(string productId)
    {
        return await _context.ProductVariants
            .Where(v => v.ProductId == productId && v.IsAvailable)
            .ToListAsync();
    }
}
