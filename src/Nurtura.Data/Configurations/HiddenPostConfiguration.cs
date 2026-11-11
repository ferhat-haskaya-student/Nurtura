using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class HiddenPostConfiguration : IEntityTypeConfiguration<HiddenPost>
{
    public void Configure(EntityTypeBuilder<HiddenPost> builder)
    {
        // Each post at most once per mother. Deleting her profile or the post deletes the hidden row.
        builder.HasIndex(s => new { s.MotherProfileId, s.ForumPostId }).IsUnique();
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(s => s.MotherProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ForumPost>()
            .WithMany()
            .HasForeignKey(s => s.ForumPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
