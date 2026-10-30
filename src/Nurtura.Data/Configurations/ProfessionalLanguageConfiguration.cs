using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ProfessionalLanguageConfiguration : IEntityTypeConfiguration<ProfessionalLanguage>
{
    public void Configure(EntityTypeBuilder<ProfessionalLanguage> builder)
    {
        builder.Property(l => l.Language)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Each language at most once per professional; deleting the profile deletes her languages.
        builder.HasIndex(l => new { l.ProfessionalProfileId, l.Language }).IsUnique();
        builder.HasOne<ProfessionalProfile>()
            .WithMany()
            .HasForeignKey(l => l.ProfessionalProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
