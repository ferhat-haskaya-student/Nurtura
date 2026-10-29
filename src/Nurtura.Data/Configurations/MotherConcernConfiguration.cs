using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class MotherConcernConfiguration : IEntityTypeConfiguration<MotherConcern>
{
    public void Configure(EntityTypeBuilder<MotherConcern> builder)
    {
        builder.Property(c => c.Concern)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Each concern at most once per mother; deleting the profile deletes her concerns.
        builder.HasIndex(c => new { c.MotherProfileId, c.Concern }).IsUnique();
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(c => c.MotherProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
