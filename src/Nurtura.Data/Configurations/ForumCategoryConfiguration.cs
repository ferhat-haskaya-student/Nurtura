using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class ForumCategoryConfiguration : IEntityTypeConfiguration<ForumCategory>
{
    public void Configure(EntityTypeBuilder<ForumCategory> builder)
    {
        builder.Property(c => c.Key)
            .IsRequired()
            .HasMaxLength(ForumCategory.KeyMaxLength);

        builder.HasIndex(c => c.Key).IsUnique();

        // The fixed list, shown in this order; Other last.
        builder.HasData(
            new ForumCategory { Id = 1, Key = "EmotionalWellbeing" },
            new ForumCategory { Id = 2, Key = "SleepAndTiredness" },
            new ForumCategory { Id = 3, Key = "BabyFeeding" },
            new ForumCategory { Id = 4, Key = "BabySleep" },
            new ForumCategory { Id = 5, Key = "Nutrition" },
            new ForumCategory { Id = 6, Key = "RecoveryAfterBirth" },
            new ForumCategory { Id = 7, Key = "GeneralSupport" },
            new ForumCategory { Id = 8, Key = "ConsultationExperiences" },
            new ForumCategory { Id = 9, Key = "Other" });
    }
}
