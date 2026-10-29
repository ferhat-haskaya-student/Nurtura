using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;

namespace Nurtura.Data.Configurations;

public class MotherSupportTypeConfiguration : IEntityTypeConfiguration<MotherSupportType>
{
    public void Configure(EntityTypeBuilder<MotherSupportType> builder)
    {
        builder.Property(s => s.SupportType)
            .HasConversion<string>()
            .HasMaxLength(30);

        // Each support type at most once per mother; deleting the profile deletes her support types.
        builder.HasIndex(s => new { s.MotherProfileId, s.SupportType }).IsUnique();
        builder.HasOne<MotherProfile>()
            .WithMany()
            .HasForeignKey(s => s.MotherProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
