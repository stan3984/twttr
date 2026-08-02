namespace twttr.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using twttr.Models;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.DisplayName).HasMaxLength(100);
        builder.Property(u => u.Email).HasMaxLength(250);
        builder.Property(u => u.PasswordHash).HasMaxLength(256);
        builder.Property(u => u.Username).HasMaxLength(100);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique();
    }
}
