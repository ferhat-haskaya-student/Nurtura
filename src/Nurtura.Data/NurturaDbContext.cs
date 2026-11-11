using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Data;

public class NurturaDbContext : IdentityDbContext<ApplicationUser>, INurturaDbContext
{
    public NurturaDbContext(DbContextOptions<NurturaDbContext> options)
        : base(options)
    {
    }

    public DbSet<MotherProfile> MotherProfiles => Set<MotherProfile>();

    public DbSet<MotherConcern> MotherConcerns => Set<MotherConcern>();

    public DbSet<MotherSupportType> MotherSupportTypes => Set<MotherSupportType>();

    public DbSet<Baby> Babies => Set<Baby>();

    public DbSet<SleepRecord> SleepRecords => Set<SleepRecord>();

    public DbSet<FeedingRecord> FeedingRecords => Set<FeedingRecord>();

    public DbSet<ProfessionalProfile> ProfessionalProfiles => Set<ProfessionalProfile>();

    public DbSet<ProfessionalCategory> ProfessionalCategories => Set<ProfessionalCategory>();

    public DbSet<ProfessionalLanguage> ProfessionalLanguages => Set<ProfessionalLanguage>();

    public DbSet<DailyCheckIn> DailyCheckIns => Set<DailyCheckIn>();

    public DbSet<FeelingTag> FeelingTags => Set<FeelingTag>();

    public DbSet<CheckInFeelingTag> CheckInFeelingTags => Set<CheckInFeelingTag>();

    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<ConsultationNote> ConsultationNotes => Set<ConsultationNote>();

    public DbSet<GuidanceCategory> GuidanceCategories => Set<GuidanceCategory>();

    public DbSet<GuidanceContent> GuidanceContents => Set<GuidanceContent>();

    public DbSet<GuidanceSource> GuidanceSources => Set<GuidanceSource>();

    public DbSet<ContentKeywordTag> ContentKeywordTags => Set<ContentKeywordTag>();

    public DbSet<SavedGuidance> SavedGuidances => Set<SavedGuidance>();

    public DbSet<AssistantQuery> AssistantQueries => Set<AssistantQuery>();

    public DbSet<ForumCategory> ForumCategories => Set<ForumCategory>();

    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();

    public DbSet<ForumComment> ForumComments => Set<ForumComment>();

    public DbSet<PostReport> PostReports => Set<PostReport>();

    public DbSet<CommentReport> CommentReports => Set<CommentReport>();

    public DbSet<HiddenPost> HiddenPosts => Set<HiddenPost>();

    public DbSet<SavedPost> SavedPosts => Set<SavedPost>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The Nurtura Assistant's full-text search ignores Polish letters (see PostgresKeywordMatcher).
        builder.HasPostgresExtension("unaccent");
        builder.ApplyConfigurationsFromAssembly(typeof(NurturaDbContext).Assembly);
    }
}
