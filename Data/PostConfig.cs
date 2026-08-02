namespace twttr.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using twttr.Models;

public class PostConfig : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Content)
               .HasMaxLength(280);

        builder.HasOne(p => p.Author)
               .WithMany(u => u.Posts)
               .HasForeignKey(p => p.AuthorId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.InReplyTo)
               .WithMany(p => p.Replies)
               .HasForeignKey(p => p.InReplyToId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => new { p.AuthorId, p.CreatedAt });
        builder.HasIndex(p => new { p.InReplyToId });
    }
}
