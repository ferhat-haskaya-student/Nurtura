using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class FeelingTagConfiguration : IEntityTypeConfiguration<FeelingTag>
{
    public void Configure(EntityTypeBuilder<FeelingTag> builder)
    {
        builder.Property(t => t.Key)
            .IsRequired()
            .HasMaxLength(FeelingTag.KeyMaxLength);

        builder.HasIndex(t => t.Key).IsUnique();

        // The eight fixed tags, shown in this order.
        builder.HasData(
            new FeelingTag { Id = 1, Key = "Tired" },
            new FeelingTag { Id = 2, Key = "Anxious" },
            new FeelingTag { Id = 3, Key = "Overwhelmed" },
            new FeelingTag { Id = 4, Key = "Sad" },
            new FeelingTag { Id = 5, Key = "Lonely" },
            new FeelingTag { Id = 6, Key = "Calm" },
            new FeelingTag { Id = 7, Key = "Happy" },
            new FeelingTag { Id = 8, Key = "Supported" });
    }
}
