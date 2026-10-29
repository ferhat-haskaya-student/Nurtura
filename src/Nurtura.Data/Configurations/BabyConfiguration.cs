using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class BabyConfiguration : IEntityTypeConfiguration<Baby>
{
    public void Configure(EntityTypeBuilder<Baby> builder)
    {
        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(Baby.NameMaxLength);

        builder.Property(b => b.BirthType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(b => b.FeedingType)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Deleting the mother's profile deletes her babies.
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(b => b.MotherProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
