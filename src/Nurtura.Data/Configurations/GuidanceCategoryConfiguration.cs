using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class GuidanceCategoryConfiguration : IEntityTypeConfiguration<GuidanceCategory>
{
    public void Configure(EntityTypeBuilder<GuidanceCategory> builder)
    {
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(GuidanceCategory.NameMaxLength);

        builder.HasIndex(c => c.Name).IsUnique();
    }
}
