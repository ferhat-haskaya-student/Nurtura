using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ForumPostConfiguration : IEntityTypeConfiguration<ForumPost>
{
    public void Configure(EntityTypeBuilder<ForumPost> builder)
    {
        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(ForumPost.TitleMaxLength);

        builder.Property(p => p.Body)
            .IsRequired()
            .HasMaxLength(ForumPost.BodyMaxLength);

        builder.Property(p => p.Language)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Deleting the author's profile keeps the post with no author ("deleted user").
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(p => p.AuthorProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ForumCategory>()
            .WithMany()
            .HasForeignKey(p => p.ForumCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The list is newest first, with or without a category; My posts by author.
        builder.HasIndex(p => p.CreatedAt);
        builder.HasIndex(p => new { p.ForumCategoryId, p.CreatedAt });
        builder.HasIndex(p => p.AuthorProfileId);
    }
}
