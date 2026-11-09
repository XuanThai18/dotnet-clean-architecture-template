using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using YourApp.Domain.Products;

namespace YourApp.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(p => p.Sku).HasMaxLength(Product.SkuMaxLength).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 2);

        // Unique chỉ trong các bản ghi chưa xoá: xoá mềm xong được tạo lại cùng SKU.
        builder.HasIndex(p => p.Sku)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName(ProductConstraints.SkuUniqueIndex);
    }
}

internal static class ProductConstraints
{
    public const string SkuUniqueIndex = "ux_products_sku";
}
