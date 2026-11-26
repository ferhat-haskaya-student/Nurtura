using Nurtura.Core.Entities;

namespace Nurtura.Core.Demo;

// Everything the demo seeder writes, built in memory by DemoDataGenerator. The entities are linked by object
// references, not ids: DemoSeeder saves them in stages and fills in the ids as they are created.
public sealed class DemoData
{
    public required DateTime NowUtc { get; init; }

    // Poland date of NowUtc.
    public required DateOnly Today { get; init; }

    // The first day of the 3 months.
    public required DateOnly FirstDay { get; init; }

    public List<DemoMother> Mothers { get; } = [];

    public List<DemoProfessional> Professionals { get; } = [];

    public List<DemoAppointment> Appointments { get; } = [];

    public List<DemoPost> Posts { get; } = [];

    public List<DemoSavedPost> SavedPosts { get; } = [];

    public List<DemoCommentReport> CommentReports { get; } = [];

    public List<DemoNotification> Notifications { get; } = [];

    public List<AssistantQuery> AssistantQueries { get; } = [];

    public IEnumerable<string> Emails =>
        Mothers.Select(m => m.Email).Concat(Professionals.Select(p => p.Email));
}

public sealed class DemoMother
{
    public required string Email { get; init; }

    public required MotherProfile Profile { get; init; }

    public required List<Topic> Concerns { get; init; }

    public required List<SupportType> SupportTypes { get; init; }

    public required Baby Baby { get; init; }

    public List<SleepRecord> Sleeps { get; } = [];

    public List<FeedingRecord> Feeds { get; } = [];

    public List<DailyCheckIn> CheckIns { get; } = [];
}

public sealed class DemoProfessional
{
    public required string Email { get; init; }

    public required ProfessionalProfile Profile { get; init; }

    public required List<Topic> Topics { get; init; }

    public required List<Language> Languages { get; init; }

    public List<AvailabilitySlot> Slots { get; } = [];
}

public sealed class DemoAppointment
{
    public required DemoMother Mother { get; init; }

    public required DemoProfessional Professional { get; init; }

    public required AvailabilitySlot Slot { get; init; }

    public required Appointment Appointment { get; init; }

    // Baby sleep and baby feeding: the appointment is about the mother's baby.
    public bool AboutBaby => Appointment.Topic is Topic.BabySleep or Topic.BabyFeeding;

    public List<ChatMessage> Messages { get; } = [];

    public ConsultationNote? Note { get; set; }
}

public sealed class DemoPost
{
    public required DemoMother Author { get; init; }

    public required ForumPost Post { get; init; }

    public List<DemoComment> Comments { get; } = [];
}

public sealed class DemoComment
{
    public required DemoMother Author { get; init; }

    public required ForumComment Comment { get; init; }
}

public sealed class DemoSavedPost
{
    public required DemoMother Mother { get; init; }

    public required DemoPost Post { get; init; }

    public required SavedPost Saved { get; init; }
}

public sealed class DemoCommentReport
{
    public required DemoMother Reporter { get; init; }

    public required DemoComment Comment { get; init; }

    public required CommentReport Report { get; init; }
}

public sealed class DemoNotification
{
    public required string RecipientEmail { get; init; }

    public required Notification Notification { get; init; }

    public DemoAppointment? Appointment { get; init; }

    public DemoPost? Post { get; init; }

    public DemoComment? Comment { get; init; }
}
