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

        // Вид хранится текстом, как и все перечисления. У уже существующих категорий — Room (см. миграцию).
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
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

internal sealed class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    public void Configure(EntityTypeBuilder<Space> builder)
    {
        builder.ToTable("spaces", table =>
        {
            table.HasCheckConstraint("ck_spaces_layout", "rows > 0 AND columns > 0");
            table.HasCheckConstraint("ck_spaces_aisle", "aisle_after_column IS NULL OR (aisle_after_column > 0 AND aisle_after_column < columns)");
        });

        builder.Property(s => s.Name).HasMaxLength(Space.NameMaxLength).UseCollation(PostgresCollations.Russian);
        builder.Property(s => s.Description).HasMaxLength(Space.DescriptionMaxLength);

        builder.HasOne(s => s.Location).WithMany().HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public const string SeatNumberIndexName = "ix_resources_space_id_seat_number";
    public const string SeatPositionIndexName = "ix_resources_space_id_seat_row_seat_column";

    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        // CHECK-ограничения дублируют правила валидаторов на уровне базы:
        // даже если в код закрадётся ошибка, некорректная строка не будет сохранена.
        builder.ToTable("resources", table =>
        {
            table.HasCheckConstraint("ck_resources_capacity_positive", "capacity IS NULL OR capacity > 0");
            table.HasCheckConstraint("ck_resources_working_hours", "close_time > open_time");
            table.HasCheckConstraint("ck_resources_max_duration_positive", "max_duration_minutes IS NULL OR max_duration_minutes > 0");

            // Положение места на схеме задаётся целиком или не задаётся вовсе.
            table.HasCheckConstraint(
                "ck_resources_seat_complete",
                "(space_id IS NULL AND seat_number IS NULL AND seat_row IS NULL AND seat_column IS NULL) "
                + "OR (space_id IS NOT NULL AND seat_number > 0 AND seat_row > 0 AND seat_column > 0)");
        });

        builder.Property(r => r.Name).HasMaxLength(Resource.NameMaxLength).UseCollation(PostgresCollations.Russian);
        builder.Property(r => r.Description).HasMaxLength(Resource.DescriptionMaxLength);
        builder.Property(r => r.ImageUrl).HasMaxLength(Resource.ImageUrlMaxLength);
        builder.Property(r => r.PickupInfo).HasMaxLength(Resource.PickupInfoMaxLength);
        builder.Property(r => r.DeactivationReason).HasMaxLength(Resource.DeactivationReasonMaxLength);

        // Коворкинг с местами удалить нельзя (Restrict → 409 in_use). Номер и позиция места уникальны в своём коворкинге;
        // у остальных ресурсов space_id пуст, и для них индекс ничего не ограничивает: NULL с NULL не совпадают.
        builder.HasOne(r => r.Space).WithMany().HasForeignKey(r => r.SpaceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.SpaceId, r.SeatNumber }).IsUnique().HasDatabaseName(SeatNumberIndexName);
        builder.HasIndex(r => new { r.SpaceId, r.SeatRow, r.SeatColumn }).IsUnique().HasDatabaseName(SeatPositionIndexName);

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
