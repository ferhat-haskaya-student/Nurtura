using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ForumCommentConfiguration : IEntityTypeConfiguration<ForumComment>
{
    public void Configure(EntityTypeBuilder<ForumComment> builder)
    {
        builder.Property(c => c.Body)
            .IsRequired()
            .HasMaxLength(ForumComment.BodyMaxLength);

        // Deleting the post deletes its comments; deleting the author's profile keeps the comment ("deleted user").
        builder.HasOne<ForumPost>()
            .WithMany()
            .HasForeignKey(c => c.ForumPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(c => c.AuthorProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        // A post's comments, oldest first.
        builder.HasIndex(c => new { c.ForumPostId, c.CreatedAt });
        builder.HasIndex(c => c.AuthorProfileId);
    }
}
