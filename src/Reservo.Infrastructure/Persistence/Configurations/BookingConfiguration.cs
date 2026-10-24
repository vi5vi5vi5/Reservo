using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reservo.Domain.Bookings;

namespace Reservo.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    /// <summary>
    /// Exclusion constraint против двойных броней (PROJECT.md, 5.3). EF Core такие ограничения описывать не умеет,
    /// поэтому оно создаётся SQL-командой в миграции AddBookings, а здесь живёт только его имя.
    /// </summary>
    public const string NoOverlapConstraintName = "ex_bookings_no_overlap";

    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings", table =>
        {
            table.HasCheckConstraint("ck_bookings_end_after_start", "end_at > start_at");
            table.HasCheckConstraint("ck_bookings_attendees_positive", "attendees IS NULL OR attendees > 0");
        });

        builder.Property(b => b.Title).HasMaxLength(Booking.TitleMaxLength);
        builder.Property(b => b.StatusReason).HasMaxLength(Booking.StatusReasonMaxLength);

        // Статус хранится текстом: так строки в базе читаемы, а условие exclusion constraint понятно — status IN ('Pending', 'Confirmed').
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(b => b.Resource).WithMany().HasForeignKey(b => b.ResourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.User).WithMany().HasForeignKey(b => b.UserId).OnDelete(DeleteBehavior.Restrict);

        // Индексы под частые запросы: расписание ресурса, «мои брони», фоновая задача по статусу и времени.
        builder.HasIndex(b => new { b.ResourceId, b.StartAt });
        builder.HasIndex(b => new { b.UserId, b.StartAt });
        builder.HasIndex(b => new { b.Status, b.StartAt });

        // Оптимистичная блокировка через системный столбец PostgreSQL xmin: он меняется при каждом обновлении строки.
        // EF Core добавляет к UPDATE условие «xmin не изменился», и если бронь успел поменять кто-то другой,
        // сохранение не пройдёт (DbUpdateConcurrencyException → 409 concurrency_conflict).
        // Свойство теневое (есть только в модели EF Core), чтобы не тащить детали базы в доменную сущность.
        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
