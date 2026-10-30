using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ProfessionalCategoryConfiguration : IEntityTypeConfiguration<ProfessionalCategory>
{
    public void Configure(EntityTypeBuilder<ProfessionalCategory> builder)
    {
        builder.Property(c => c.Category)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Each topic at most once per professional; deleting the profile deletes her topics.
        builder.HasIndex(c => new { c.ProfessionalProfileId, c.Category }).IsUnique();
        builder.HasOne<ProfessionalProfile>()
            .WithMany()
            .HasForeignKey(c => c.ProfessionalProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
