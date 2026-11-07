using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ContentKeywordTagConfiguration : IEntityTypeConfiguration<ContentKeywordTag>
{
    public void Configure(EntityTypeBuilder<ContentKeywordTag> builder)
    {
        builder.Property(t => t.Keyword)
            .IsRequired()
            .HasMaxLength(ContentKeywordTag.KeywordMaxLength);

        // Each keyword at most once per page; deleting the page deletes its keywords.
        builder.HasIndex(t => new { t.GuidanceContentId, t.Keyword }).IsUnique();
        builder.HasOne<GuidanceContent>()
            .WithMany()
            .HasForeignKey(t => t.GuidanceContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
