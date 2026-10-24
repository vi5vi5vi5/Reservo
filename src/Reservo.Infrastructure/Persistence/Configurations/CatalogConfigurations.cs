using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reservo.Domain.Catalog;

namespace Reservo.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public const string CodeIndexName = "ix_categories_code";
    public const string NameIndexName = "ix_categories_name";

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.Property(c => c.Code).HasMaxLength(Category.CodeMaxLength);
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName(CodeIndexName);

        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).UseCollation(PostgresCollations.Russian);
        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName(NameIndexName);

        builder.Property(c => c.Description).HasMaxLength(Category.DescriptionMaxLength);
    }
}

internal sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public const string NameIndexName = "ix_locations_name";

    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");

        builder.Property(l => l.Name).HasMaxLength(Location.NameMaxLength).UseCollation(PostgresCollations.Russian);
        builder.HasIndex(l => l.Name).IsUnique().HasDatabaseName(NameIndexName);

        builder.Property(l => l.Address).HasMaxLength(Location.AddressMaxLength);
    }
}

internal sealed class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public const string CodeIndexName = "ix_features_code";
    public const string NameIndexName = "ix_features_name";

    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");

        builder.Property(f => f.Code).HasMaxLength(Feature.CodeMaxLength);
        builder.HasIndex(f => f.Code).IsUnique().HasDatabaseName(CodeIndexName);

        builder.Property(f => f.Name).HasMaxLength(Feature.NameMaxLength).UseCollation(PostgresCollations.Russian);
        builder.HasIndex(f => f.Name).IsUnique().HasDatabaseName(NameIndexName);
    }
}

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        // CHECK-ограничения дублируют правила валидаторов на уровне базы:
        // даже если в код закрадётся ошибка, некорректная строка не будет сохранена.
        builder.ToTable("resources", table =>
        {
            table.HasCheckConstraint("ck_resources_capacity_positive", "capacity IS NULL OR capacity > 0");
            table.HasCheckConstraint("ck_resources_working_hours", "close_time > open_time");
            table.HasCheckConstraint("ck_resources_max_duration_positive", "max_duration_minutes IS NULL OR max_duration_minutes > 0");
        });

        builder.Property(r => r.Name).HasMaxLength(Resource.NameMaxLength).UseCollation(PostgresCollations.Russian);
        builder.Property(r => r.Description).HasMaxLength(Resource.DescriptionMaxLength);
        builder.Property(r => r.ImageUrl).HasMaxLength(Resource.ImageUrlMaxLength);

        // Restrict: категорию или корпус, в которых есть ресурсы, удалить нельзя — база вернёт ошибку,
        // а ConstraintErrors превратит её в 409 in_use.
        builder.HasOne(r => r.Category).WithMany().HasForeignKey(r => r.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Location).WithMany().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Restrict);

        // Связь «многие-ко-многим» через таблицу resource_features без отдельного класса в коде.
        // Удаление ресурса убирает его связи (Cascade), а оснащение, которое где-то используется, удалить нельзя (Restrict).
        builder.HasMany(r => r.Features)
            .WithMany()
            .UsingEntity(
                "resource_features",
                feature => feature.HasOne(typeof(Feature)).WithMany().HasForeignKey("FeatureId").OnDelete(DeleteBehavior.Restrict),
                resource => resource.HasOne(typeof(Resource)).WithMany().HasForeignKey("ResourceId").OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("ResourceId", "FeatureId"));

        builder.Navigation(r => r.Features).HasField("_features");
    }
}
