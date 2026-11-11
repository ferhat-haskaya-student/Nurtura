using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class PostReportConfiguration : IEntityTypeConfiguration<PostReport>
{
    public void Configure(EntityTypeBuilder<PostReport> builder)
    {
        builder.Property(r => r.Reason)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Note)
            .HasMaxLength(PostReport.NoteMaxLength);

        // One report per mother per post. Deleting her profile or the post deletes the report.
        builder.HasIndex(r => new { r.ReporterProfileId, r.ForumPostId }).IsUnique();
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(r => r.ReporterProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ForumPost>()
            .WithMany()
            .HasForeignKey(r => r.ForumPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // The admin review lists open reports.
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
    }
}
