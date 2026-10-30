using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Data.Configurations;

public class ProfessionalProfileConfiguration : IEntityTypeConfiguration<ProfessionalProfile>
{
    public void Configure(EntityTypeBuilder<ProfessionalProfile> builder)
    {
        builder.Property(p => p.DisplayName)
            .IsRequired()
            .HasMaxLength(ProfessionalProfile.DisplayNameMaxLength);

        builder.Property(p => p.Bio)
            .IsRequired()
            .HasMaxLength(ProfessionalProfile.BioMaxLength);

        builder.Property(p => p.ProfessionalType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(p => p.ApprovalStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        // One profile per user; deleting the user deletes the profile.
        builder.HasIndex(p => p.UserId).IsUnique();
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<ProfessionalProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
