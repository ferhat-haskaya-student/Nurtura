using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class CommentReportConfiguration : IEntityTypeConfiguration<CommentReport>
{
    public void Configure(EntityTypeBuilder<CommentReport> builder)
    {
        builder.Property(r => r.Reason)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Note)
            .HasMaxLength(CommentReport.NoteMaxLength);

        // One report per mother per comment. Deleting her profile or the comment deletes the report.
        builder.HasIndex(r => new { r.ReporterProfileId, r.ForumCommentId }).IsUnique();
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(r => r.ReporterProfileId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ForumComment>()
            .WithMany()
            .HasForeignKey(r => r.ForumCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        // The admin review lists open reports.
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
    }
}
