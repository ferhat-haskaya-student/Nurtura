using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nurtura.Core;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Data;

namespace Nurtura.Tests.Infrastructure;

public sealed record TestMother(string Email, string UserId, int ProfileId, string DisplayName, int BabyId);

public sealed record TestProfessional(string Email, string UserId, int ProfileId, string DisplayName);

public sealed record TestUsers(TestMother A, TestMother B, TestProfessional P1, TestProfessional P2, string AdminEmail);

// Seeds test data straight into nurtura_test. Every text is a unique marker, so a test can search for it.
public sealed class TestData(NurturaAppFactory factory)
{
    public const int SessionHour = 10;

    // A unique word that appears nowhere else in the database.
    public static string Marker(string label) => $"{label}{Guid.NewGuid():N}"[..Math.Min(label.Length + 12, 50)];

    // UTC, cut to whole minutes like the app stores it.
    public static DateTime Minute(DateTime utc) =>
        new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

    // A Poland date for a session that has not started yet (never a Sunday).
    public static DateOnly UpcomingDate() => NotSunday(PolandTime.Today.AddDays(2), 1);

    // A Poland date for a session that is over (never a Sunday).
    public static DateOnly PastDate() => NotSunday(PolandTime.Today.AddDays(-2), -1);

    private static DateOnly NotSunday(DateOnly date, int step) =>
        date.DayOfWeek == DayOfWeek.Sunday ? date.AddDays(step) : date;

    public async Task<T> DbAsync<T>(Func<NurturaDbContext, Task<T>> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<NurturaDbContext>());
    }

    public async Task DbAsync(Func<NurturaDbContext, Task> work)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider.GetRequiredService<NurturaDbContext>());
    }

    public async Task<T> ServiceAsync<TService, T>(Func<TService, Task<T>> work) where TService : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<TService>());
    }

    // Mother A, mother B, professionals P1 and P2 (approved, active) and the admin.
    public async Task<TestUsers> SeedUsersAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<NurturaDbContext>();

        await CreateUserAsync(users, NurturaAppFactory.AdminEmail, Roles.Admin);
        var a = await CreateMotherAsync(users, db, "mother.a@test.nurtura", Marker("AnnaA"));
        var b = await CreateMotherAsync(users, db, "mother.b@test.nurtura", Marker("BeataB"));
        var p1 = await CreateProfessionalAsync(users, db, "pro.one@test.nurtura", Marker("PaulaOne"));
        var p2 = await CreateProfessionalAsync(users, db, "pro.two@test.nurtura", Marker("PiaTwo"));
        return new TestUsers(a, b, p1, p2, NurturaAppFactory.AdminEmail);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users, string email, string role)
    {
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        Check(await users.CreateAsync(user, NurturaAppFactory.Password));
        Check(await users.AddToRoleAsync(user, role));
        return user;
    }

    private static async Task<TestMother> CreateMotherAsync(
        UserManager<ApplicationUser> users, NurturaDbContext db, string email, string displayName)
    {
        var user = await CreateUserAsync(users, email, Roles.Mother);
        var profile = new MotherProfile
        {
            UserId = user.Id,
            DisplayName = displayName,
            IsFirstBaby = true,
            HelpAtHome = HelpAtHome.Sometimes,
            OnboardingCompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.MotherProfiles.Add(profile);
        await db.SaveChangesAsync();

        db.MotherConcerns.Add(new MotherConcern { MotherProfileId = profile.Id, Concern = Topic.BabySleep });
        db.MotherSupportTypes.Add(new MotherSupportType { MotherProfileId = profile.Id, SupportType = SupportType.TalkingToProfessional });
        var baby = new Baby
        {
            MotherProfileId = profile.Id,
            Name = "Baby of " + displayName,
            BirthDate = PolandTime.Today.AddMonths(-3),
            BirthType = BirthType.Natural,
            BirthWeek = Baby.BirthWeekDefault,
            FeedingType = BabyFeedingType.Breast
        };
        db.Babies.Add(baby);
        await db.SaveChangesAsync();

        return new TestMother(email, user.Id, profile.Id, displayName, baby.Id);
    }

    private static async Task<TestProfessional> CreateProfessionalAsync(
        UserManager<ApplicationUser> users, NurturaDbContext db, string email, string displayName)
    {
        var user = await CreateUserAsync(users, email, Roles.Professional);
        var profile = new ProfessionalProfile
        {
            UserId = user.Id,
            DisplayName = displayName,
            ProfessionalType = ProfessionalType.Midwife,
            Bio = "Test professional.",
            ApprovalStatus = ApprovalStatus.Approved,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.ProfessionalProfiles.Add(profile);
        await db.SaveChangesAsync();

        db.ProfessionalCategories.AddRange(Enum.GetValues<Topic>()
            .Select(t => new ProfessionalCategory { ProfessionalProfileId = profile.Id, Category = t }));
        db.ProfessionalLanguages.Add(new ProfessionalLanguage { ProfessionalProfileId = profile.Id, Language = Language.English });
        await db.SaveChangesAsync();

        return new TestProfessional(email, user.Id, profile.Id, displayName);
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    public Task<int> AddCheckInAsync(int motherId, DateOnly date, string? note, bool shared) =>
        DbAsync(async db =>
        {
            var checkIn = new DailyCheckIn
            {
                MotherProfileId = motherId, Date = date, Mood = 2, SleepQuality = 2, Energy = 2, Stress = 4,
                Note = note, IsShared = shared, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                FeelingTags = [new CheckInFeelingTag { FeelingTagId = 1 }]
            };
            db.DailyCheckIns.Add(checkIn);
            await db.SaveChangesAsync();
            return checkIn.Id;
        });

    public Task<int> AddSleepAsync(int babyId, DateTime startUtc, TimeSpan length, string? note, bool shared) =>
        DbAsync(async db =>
        {
            var sleep = new SleepRecord
            {
                BabyId = babyId, StartTime = Minute(startUtc), EndTime = Minute(startUtc + length), Note = note,
                IsShared = shared, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            db.SleepRecords.Add(sleep);
            await db.SaveChangesAsync();
            return sleep.Id;
        });

    // A sleep with no end time: the timer runs.
    public Task<int> AddRunningSleepAsync(int babyId, DateTime startUtc) =>
        DbAsync(async db =>
        {
            var sleep = new SleepRecord
            {
                BabyId = babyId, StartTime = Minute(startUtc), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            db.SleepRecords.Add(sleep);
            await db.SaveChangesAsync();
            return sleep.Id;
        });

    // Every sleep in the database, as one comparable string.
    public Task<string> SleepSnapshotAsync() =>
        DbAsync(async db =>
        {
            var sleeps = await db.SleepRecords.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
            return string.Join("\n", sleeps.Select(s =>
                $"{s.Id}|{s.BabyId}|{s.StartTime:O}|{s.EndTime:O}|{s.Note}|{s.IsShared}|{s.UpdatedAt:O}"));
        });

    public Task<int> AddFeedAsync(int babyId, DateTime timeUtc, string? note, bool shared) =>
        DbAsync(async db =>
        {
            var feed = new FeedingRecord
            {
                BabyId = babyId, Time = Minute(timeUtc), Type = FeedingType.Breast, BreastSide = BreastSide.Left,
                Minutes = 10, Note = note, IsShared = shared, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            db.FeedingRecords.Add(feed);
            await db.SaveChangesAsync();
            return feed.Id;
        });

    // An appointment on a new slot of the professional at the given Poland date and hour.
    public Task<int> AddAppointmentAsync(
        int motherId, int professionalId, DateOnly date, AppointmentStatus status,
        Topic topic = Topic.PostpartumWellbeing, string? concern = null, int? babyId = null,
        DateTime? sessionEndedAtUtc = null, int hour = SessionHour) =>
        DbAsync(async db =>
        {
            var slot = new AvailabilitySlot
            {
                ProfessionalProfileId = professionalId, Date = date, StartHour = hour, IsOpen = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            db.AvailabilitySlots.Add(slot);
            await db.SaveChangesAsync();

            var appointment = new Appointment
            {
                MotherProfileId = motherId, AvailabilitySlotId = slot.Id, Topic = topic, BabyId = babyId,
                ConcernText = concern ?? Marker("Concern"), Status = status,
                SessionEndedAt = sessionEndedAtUtc,
                SessionEndedBy = sessionEndedAtUtc is null ? null : AppointmentParty.Professional,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            return appointment.Id;
        });

    public Task<int> AddChatMessageAsync(int appointmentId, AppointmentParty sender, string text) =>
        DbAsync(async db =>
        {
            var message = new ChatMessage { AppointmentId = appointmentId, Sender = sender, Text = text, SentAt = DateTime.UtcNow };
            db.ChatMessages.Add(message);
            await db.SaveChangesAsync();
            return message.Id;
        });

    public Task<int> AddClosingNoteAsync(int appointmentId, string text) =>
        DbAsync(async db =>
        {
            var note = new ConsultationNote
            {
                AppointmentId = appointmentId, Text = text, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            db.ConsultationNotes.Add(note);
            await db.SaveChangesAsync();
            return note.Id;
        });

    public Task<int> AddNotificationAsync(
        string userId, NotificationType type, string otherName, int? appointmentId = null, int? postId = null,
        int? commentId = null, string? postTitle = null) =>
        DbAsync(async db =>
        {
            var notification = new Notification
            {
                UserId = userId, Type = type, OtherName = otherName, AppointmentId = appointmentId,
                ForumPostId = postId, ForumCommentId = commentId, PostTitle = postTitle,
                Date = appointmentId is null ? null : UpcomingDate(), Hour = appointmentId is null ? null : SessionHour,
                CreatedAt = DateTime.UtcNow
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            return notification.Id;
        });

    public Task<int> AddPostAsync(int authorId, string title, string body, DateTime? createdAtUtc = null) =>
        DbAsync(async db =>
        {
            var post = new ForumPost
            {
                AuthorProfileId = authorId, ForumCategoryId = 1, Title = title, Body = body,
                Language = Language.English, CreatedAt = createdAtUtc ?? DateTime.UtcNow
            };
            db.ForumPosts.Add(post);
            await db.SaveChangesAsync();
            return post.Id;
        });

    public Task<int> AddCommentAsync(int postId, int authorId, string body, DateTime? createdAtUtc = null) =>
        DbAsync(async db =>
        {
            var comment = new ForumComment
            {
                ForumPostId = postId, AuthorProfileId = authorId, Body = body, CreatedAt = createdAtUtc ?? DateTime.UtcNow
            };
            db.ForumComments.Add(comment);
            await db.SaveChangesAsync();
            return comment.Id;
        });

    public Task AddPostReportAsync(int reporterId, int postId, string? note) =>
        DbAsync(async db =>
        {
            db.PostReports.Add(new PostReport
            {
                ReporterProfileId = reporterId, ForumPostId = postId, Reason = ReportReason.Unkind, Note = note,
                Status = ReportStatus.Open, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

    public Task AddCommentReportAsync(int reporterId, int commentId, string? note) =>
        DbAsync(async db =>
        {
            db.CommentReports.Add(new CommentReport
            {
                ReporterProfileId = reporterId, ForumCommentId = commentId, Reason = ReportReason.Unkind, Note = note,
                Status = ReportStatus.Open, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
}
