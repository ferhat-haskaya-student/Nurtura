using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Core;

public interface INurturaDbContext
{
    DbSet<MotherProfile> MotherProfiles { get; }

    DbSet<MotherConcern> MotherConcerns { get; }

    DbSet<MotherSupportType> MotherSupportTypes { get; }

    DbSet<Baby> Babies { get; }

    DbSet<SleepRecord> SleepRecords { get; }

    DbSet<FeedingRecord> FeedingRecords { get; }

    DbSet<ProfessionalProfile> ProfessionalProfiles { get; }

    DbSet<ProfessionalCategory> ProfessionalCategories { get; }

    DbSet<ProfessionalLanguage> ProfessionalLanguages { get; }

    DbSet<DailyCheckIn> DailyCheckIns { get; }

    DbSet<FeelingTag> FeelingTags { get; }

    DbSet<CheckInFeelingTag> CheckInFeelingTags { get; }

    DbSet<AvailabilitySlot> AvailabilitySlots { get; }

    DbSet<Appointment> Appointments { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<ChatMessage> ChatMessages { get; }

    DbSet<ConsultationNote> ConsultationNotes { get; }

    DbSet<GuidanceCategory> GuidanceCategories { get; }

    DbSet<GuidanceContent> GuidanceContents { get; }

    DbSet<GuidanceSource> GuidanceSources { get; }

    DbSet<ContentKeywordTag> ContentKeywordTags { get; }

    DbSet<SavedGuidance> SavedGuidances { get; }

    // Identity's user table; implemented by IdentityDbContext.
    DbSet<ApplicationUser> Users { get; }

    // Used for transactions that span Identity and Nurtura tables.
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
