using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reservo.Domain.Audit;
using Reservo.Domain.Notifications;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.EntityType).HasMaxLength(AuditLog.EntityTypeMaxLength);

        // jsonb — JSON, который PostgreSQL хранит в разобранном виде: по нему можно искать и строить индексы.
        builder.Property(a => a.Details).HasColumnType("jsonb");

        // Автор — необязательная ссылка: у действий системы (фоновой задачи) его нет.
        builder.HasOne(a => a.Actor).WithMany().HasForeignKey(a => a.ActorId).OnDelete(DeleteBehavior.Restrict);

        // Индексы под запросы журнала: история одной сущности, действия одного человека, свежие записи.
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.CreatedAt);
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(n => n.Title).HasMaxLength(Notification.TitleMaxLength);
        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength);

        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Bookings.Booking>().WithMany().HasForeignKey(n => n.BookingId).OnDelete(DeleteBehavior.Restrict);

        // Главный запрос: «мои непрочитанные, свежие сверху» — индекс покрывает его целиком.
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.Property(t => t.TokenHash).HasMaxLength(RefreshToken.TokenHashMaxLength);
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.Property(t => t.RevokedReason).HasConversion<string>().HasMaxLength(20);

        // Cascade: сессии принадлежат пользователю целиком (PROJECT.md, 6.4).
        builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
