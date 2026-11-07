using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class GuidanceSourceConfiguration : IEntityTypeConfiguration<GuidanceSource>
{
    public void Configure(EntityTypeBuilder<GuidanceSource> builder)
    {
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(GuidanceSource.NameMaxLength);

        builder.Property(s => s.Url)
            .IsRequired()
            .HasMaxLength(GuidanceSource.UrlMaxLength);
    }
}
