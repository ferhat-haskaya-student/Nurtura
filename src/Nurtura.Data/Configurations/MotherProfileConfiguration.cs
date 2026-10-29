using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Data.Configurations;

public class MotherProfileConfiguration : IEntityTypeConfiguration<MotherProfile>
{
    public void Configure(EntityTypeBuilder<MotherProfile> builder)
    {
        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(MotherProfile.DisplayNameMaxLength);

        builder.Property(p => p.HelpAtHome)
            .HasConversion<string>()
            .HasMaxLength(30);

        // One profile per user; deleting the user deletes the profile.
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<MotherProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
