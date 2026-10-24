using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const string EmailIndexName = "ix_users_email";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength);
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(EmailIndexName);

        builder.Property(u => u.FullName).HasMaxLength(User.FullNameMaxLength);

        builder.Property(u => u.PasswordHash);

        // Stored as text ("User", "Admin") so rows stay readable in the database.
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
    }
}
