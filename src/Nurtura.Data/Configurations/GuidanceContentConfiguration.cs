using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class GuidanceContentConfiguration : IEntityTypeConfiguration<GuidanceContent>
{
    public void Configure(EntityTypeBuilder<GuidanceContent> builder)
    {
        builder.Property(g => g.Slug)
            .IsRequired()
            .HasMaxLength(GuidanceContent.SlugMaxLength);

        // The seeder recognises a page by its slug, so a renamed page is never added twice.
        builder.HasIndex(g => g.Slug).IsUnique();

        builder.Property(g => g.Audience)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(g => g.Title)
            .IsRequired()
            .HasMaxLength(GuidanceContent.TitleMaxLength);

        builder.Property(g => g.Summary)
            .IsRequired()
            .HasMaxLength(GuidanceContent.SummaryMaxLength);

        builder.Property(g => g.Body).IsRequired();

        builder.Property(g => g.RelatedTopic)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(g => g.GetHelpHeading)
            .IsRequired()
            .HasMaxLength(GuidanceContent.HeadingMaxLength);

        builder.Property(g => g.EmergencyHeading)
            .HasMaxLength(GuidanceContent.HeadingMaxLength);

        builder.Property(g => g.Language)
            .HasConversion<string>()
            .HasMaxLength(30);

        // A category with pages cannot be deleted.
        builder.HasOne(g => g.Category)
            .WithMany()
            .HasForeignKey(g => g.GuidanceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(g => g.Sources)
            .WithOne()
            .HasForeignKey(s => s.GuidanceContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
