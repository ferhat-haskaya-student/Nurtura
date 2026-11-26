using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nurtura.Core.Demo;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Data;
using Nurtura.Tests.Infrastructure;

namespace Nurtura.Tests.Rules;

// --reset-demo removes every @demo.nurtura account and everything of it, then seeds again (design doc §4 "Demo data",
// "Reset"). These tests call the seeder itself on nurtura_test, which is emptied before every test; the command-line
// flags are never used here, and their guard refuses nurtura_test.
public sealed class DemoResetTests(NurturaAppFactory factory) : IntegrationTest(factory)
{
    private const string DemoDomain = "@DEMO.NURTURA";

    private async Task<DemoSeedResult> RunAsync(bool reset, DateTime nowUtc)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<NurturaDbContext>();
        return reset
            ? await DemoSeeder.ResetAsync(users, db, NurturaAppFactory.Password, nowUtc)
            : await DemoSeeder.SeedAsync(users, db, NurturaAppFactory.Password, nowUtc);
    }

    private Task<int> AddAssistantQueryAsync(AssistantQuery query) =>
        Data.DbAsync(async db =>
        {
            var copy = new AssistantQuery
            {
                Date = query.Date, Matched = query.Matched, SuggestedTopic = query.SuggestedTopic,
                UrgentShown = query.UrgentShown
            };
            db.AssistantQueries.Add(copy);
            await db.SaveChangesAsync();
            return copy.Id;
        });

    // Everything of the accounts that are not demo accounts, as one comparable string.
    private Task<string> OthersSnapshotAsync() =>
        Data.DbAsync(async db =>
        {
            var users = await db.Users.AsNoTracking()
                .Where(u => !u.NormalizedEmail!.EndsWith(DemoDomain))
                .OrderBy(u => u.Id).ToListAsync();
            var userIds = users.Select(u => u.Id).ToList();
            var mothers = await db.MotherProfiles.AsNoTracking()
                .Where(p => userIds.Contains(p.UserId)).OrderBy(p => p.Id).ToListAsync();
            var motherIds = mothers.Select(m => m.Id).ToList();
            var professionals = await db.ProfessionalProfiles.AsNoTracking()
                .Where(p => userIds.Contains(p.UserId)).OrderBy(p => p.Id).ToListAsync();
            var professionalIds = professionals.Select(p => p.Id).ToList();
            var babies = await db.Babies.AsNoTracking()
                .Where(b => motherIds.Contains(b.MotherProfileId)).OrderBy(b => b.Id).ToListAsync();
            var babyIds = babies.Select(b => b.Id).ToList();

            var lines = new List<string>();
            lines.AddRange(users.Select(u => $"user {u.Id} {u.Email} {u.PasswordHash}"));
            lines.AddRange(mothers.Select(m => $"mother {m.Id} {m.DisplayName} {m.OnboardingCompletedAt:O}"));
            lines.AddRange(professionals.Select(p => $"professional {p.Id} {p.DisplayName} {p.ApprovalStatus}"));
            lines.AddRange(babies.Select(b => $"baby {b.Id} {b.Name} {b.BirthDate}"));
            lines.AddRange((await db.SleepRecords.AsNoTracking().Where(s => babyIds.Contains(s.BabyId))
                .OrderBy(s => s.Id).ToListAsync()).Select(s => $"sleep {s.Id} {s.StartTime:O} {s.EndTime:O} {s.IsShared}"));
            lines.AddRange((await db.FeedingRecords.AsNoTracking().Where(f => babyIds.Contains(f.BabyId))
                .OrderBy(f => f.Id).ToListAsync()).Select(f => $"feed {f.Id} {f.Time:O} {f.IsShared}"));
            lines.AddRange((await db.DailyCheckIns.AsNoTracking().Include(c => c.FeelingTags)
                .Where(c => motherIds.Contains(c.MotherProfileId)).OrderBy(c => c.Id).ToListAsync())
                .Select(c => $"check-in {c.Id} {c.Date} {c.Mood} {c.Note} {c.FeelingTags.Count} {c.IsShared}"));
            lines.AddRange((await db.AvailabilitySlots.AsNoTracking()
                .Where(s => professionalIds.Contains(s.ProfessionalProfileId)).OrderBy(s => s.Id).ToListAsync())
                .Select(s => $"slot {s.Id} {s.Date} {s.StartHour} {s.IsOpen}"));
            var appointments = await db.Appointments.AsNoTracking()
                .Where(a => motherIds.Contains(a.MotherProfileId)).OrderBy(a => a.Id).ToListAsync();
            var appointmentIds = appointments.Select(a => a.Id).ToList();
            lines.AddRange(appointments.Select(a => $"appointment {a.Id} {a.AvailabilitySlotId} {a.Status} {a.ConcernText}"));
            lines.AddRange((await db.ChatMessages.AsNoTracking()
                .Where(m => appointmentIds.Contains(m.AppointmentId)).OrderBy(m => m.Id).ToListAsync())
                .Select(m => $"chat {m.Id} {m.AppointmentId} {m.Text}"));
            lines.AddRange((await db.ForumPosts.AsNoTracking()
                .Where(p => p.AuthorProfileId != null && motherIds.Contains(p.AuthorProfileId.Value))
                .OrderBy(p => p.Id).ToListAsync()).Select(p => $"post {p.Id} {p.Title}"));
            lines.AddRange((await db.ForumComments.AsNoTracking()
                .Where(c => c.AuthorProfileId != null && motherIds.Contains(c.AuthorProfileId.Value))
                .OrderBy(c => c.Id).ToListAsync()).Select(c => $"comment {c.Id} {c.ForumPostId} {c.Body}"));
            lines.AddRange((await db.Notifications.AsNoTracking()
                .Where(n => userIds.Contains(n.UserId)).OrderBy(n => n.Id).ToListAsync())
                .Select(n => $"notification {n.Id} {n.Type} {n.OtherName}"));
            return string.Join("\n", lines);
        });

    private Task<List<string>> DemoEmailsAsync() =>
        Data.DbAsync(db => db.Users.Where(u => u.NormalizedEmail!.EndsWith(DemoDomain))
            .Select(u => u.Email!).OrderBy(e => e).ToListAsync());

    [Fact]
    public async Task Reset_RemovesOnlyTheDemoAccounts_OtherAccountsUntouched()
    {
        var now = DateTime.UtcNow;

        // The test accounts with records of their own, among themselves.
        await Data.AddCheckInAsync(Users.A.ProfileId, PolandTimeToday(), "A's own note", shared: false);
        await Data.AddSleepAsync(Users.A.BabyId, now.AddHours(-5), TimeSpan.FromHours(2), null, shared: false);
        await Data.AddFeedAsync(Users.A.BabyId, now.AddHours(-2), null, shared: true);
        var postB = await Data.AddPostAsync(Users.B.ProfileId, "B's post", "B's body");
        await Data.AddCommentAsync(postB, Users.A.ProfileId, "A's comment");
        var appointment = await Data.AddAppointmentAsync(
            Users.A.ProfileId, Users.P1.ProfileId, TestData.UpcomingDate(), AppointmentStatus.Confirmed);
        await Data.AddChatMessageAsync(appointment, AppointmentParty.Mother, "A's message");
        await Data.AddNotificationAsync(Users.A.UserId, NotificationType.RequestAccepted, "P1", appointment);

        await RunAsync(reset: false, now.AddDays(-4));
        var before = await OthersSnapshotAsync();

        var result = await RunAsync(reset: true, now);

        Assert.True(result.Seeded, string.Join("\n", result.Lines));
        Assert.Equal(before, await OthersSnapshotAsync());
        Assert.Equal(DemoAccounts.All.Order(), await DemoEmailsAsync());
    }

    [Fact]
    public async Task Reset_Twice_LeavesExactlyOneSetOfDemoData()
    {
        var now = DateTime.UtcNow;
        await RunAsync(reset: false, now);
        await RunAsync(reset: true, now);
        var result = await RunAsync(reset: true, now);
        Assert.True(result.Seeded);

        var expected = DemoDataGenerator.Generate(now);
        await Data.DbAsync(async db =>
        {
            Assert.Equal(DemoAccounts.All.Order(), await DemoEmailsAsync());
            Assert.Equal(3 + 2, await db.MotherProfiles.CountAsync()); // the demo mothers and test mothers A and B
            Assert.Equal(expected.Mothers.Sum(m => m.Sleeps.Count), await db.SleepRecords.CountAsync());
            Assert.Equal(expected.Mothers.Sum(m => m.Feeds.Count), await db.FeedingRecords.CountAsync());
            Assert.Equal(expected.Mothers.Sum(m => m.CheckIns.Count), await db.DailyCheckIns.CountAsync());
            Assert.Equal(expected.Professionals.Sum(p => p.Slots.Count), await db.AvailabilitySlots.CountAsync());
            Assert.Equal(expected.Appointments.Count, await db.Appointments.CountAsync());
            Assert.Equal(expected.Appointments.Sum(a => a.Messages.Count), await db.ChatMessages.CountAsync());
            Assert.Equal(expected.Posts.Count, await db.ForumPosts.CountAsync());
            Assert.Equal(expected.Posts.Sum(p => p.Comments.Count), await db.ForumComments.CountAsync());
            Assert.Equal(expected.Notifications.Count, await db.Notifications.CountAsync());
            Assert.Equal(1, await db.CommentReports.CountAsync());
            Assert.Equal(1, await db.SavedPosts.CountAsync());
            Assert.Equal(expected.AssistantQueries.Count, await db.AssistantQueries.CountAsync());
        });
    }

    [Fact]
    public async Task Reset_RemovesExactlyTheEarlierDemoAssistantSearches()
    {
        // The earlier run, a few days ago; other searches with the very same values before and after it.
        var earlier = DateTime.UtcNow.AddDays(-5);
        var earlierRows = DemoDataGenerator.AssistantQueries(DemoTimeToday(earlier));
        var others = new List<int>();
        foreach (var row in earlierRows.Take(5))
        {
            others.Add(await AddAssistantQueryAsync(row));
        }

        await RunAsync(reset: false, earlier);
        foreach (var row in earlierRows.TakeLast(5))
        {
            others.Add(await AddAssistantQueryAsync(row));
        }

        var demoIds = await Data.DbAsync(db =>
            db.AssistantQueries.Where(q => !others.Contains(q.Id)).Select(q => q.Id).ToListAsync());
        Assert.Equal(40, demoIds.Count);

        var result = await RunAsync(reset: true, DateTime.UtcNow);

        Assert.Contains(result.Lines, l => l.Contains("40 Assistant searches"));
        await Data.DbAsync(async db =>
        {
            Assert.Equal(others.Count, await db.AssistantQueries.CountAsync(q => others.Contains(q.Id)));
            Assert.Equal(0, await db.AssistantQueries.CountAsync(q => demoIds.Contains(q.Id)));
            Assert.Equal(others.Count + 40, await db.AssistantQueries.CountAsync());
        });
    }

    [Fact]
    public async Task OldNamedDemoAccounts_StopTheSeed_AndGoWithTheReset()
    {
        // Accounts of the first run, with its names.
        var oldMotherId = await Data.DbAsync(async db =>
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var mother = new ApplicationUser { UserName = "kasia@demo.nurtura", Email = "kasia@demo.nurtura", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(mother, NurturaAppFactory.Password)).Succeeded);
            var professional = new ApplicationUser { UserName = "anna@demo.nurtura", Email = "anna@demo.nurtura", EmailConfirmed = true };
            Assert.True((await users.CreateAsync(professional, NurturaAppFactory.Password)).Succeeded);

            var profile = new MotherProfile { UserId = mother.Id, DisplayName = "Kasia", CreatedAt = DateTime.UtcNow };
            db.MotherProfiles.Add(profile);
            var pro = new ProfessionalProfile
            {
                UserId = professional.Id, DisplayName = "Anna Nowak", Bio = "Midwife.",
                ApprovalStatus = ApprovalStatus.Approved, CreatedAt = DateTime.UtcNow
            };
            db.ProfessionalProfiles.Add(pro);
            await db.SaveChangesAsync();
            db.Babies.Add(new Baby
            {
                MotherProfileId = profile.Id, Name = "Zosia", BirthDate = PolandTimeToday().AddMonths(-3),
                BirthWeek = 40
            });
            db.AvailabilitySlots.Add(new AvailabilitySlot
            {
                ProfessionalProfileId = pro.Id, Date = TestData.UpcomingDate(), StartHour = 10, IsOpen = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return profile.Id;
        });
        await Data.AddCheckInAsync(oldMotherId, PolandTimeToday(), "Old run", shared: false);

        // --seed-demo adds nothing next to them.
        var seed = await RunAsync(reset: false, DateTime.UtcNow);
        Assert.False(seed.Seeded);
        Assert.Equal(["anna@demo.nurtura", "kasia@demo.nurtura"], await DemoEmailsAsync());

        // --reset-demo replaces them.
        var reset = await RunAsync(reset: true, DateTime.UtcNow);
        Assert.True(reset.Seeded, string.Join("\n", reset.Lines));
        Assert.Equal(DemoAccounts.All.Order(), await DemoEmailsAsync());
        await Data.DbAsync(async db =>
        {
            Assert.False(await db.MotherProfiles.AnyAsync(p => p.Id == oldMotherId));
            Assert.False(await db.Babies.AnyAsync(b => b.Name == "Zosia"));
            Assert.False(await db.DailyCheckIns.AnyAsync(c => c.Note == "Old run"));
            Assert.False(await db.ProfessionalProfiles.AnyAsync(p => p.DisplayName == "Anna Nowak"));
        });
    }

    [Fact]
    public async Task Reset_IsRefused_WhenATestAccountIsLinkedToTheDemoData()
    {
        var now = DateTime.UtcNow;
        await RunAsync(reset: false, now);

        // Test mother A commented on a demo post.
        var demoPostId = await Data.DbAsync(db => db.ForumPosts.Select(p => p.Id).FirstAsync());
        await Data.AddCommentAsync(demoPostId, Users.A.ProfileId, "A on a demo post");
        var demoBefore = await Data.DbAsync(db => db.Users.Where(u => u.NormalizedEmail!.EndsWith(DemoDomain))
            .OrderBy(u => u.Id).Select(u => u.Id).ToListAsync());
        var othersBefore = await OthersSnapshotAsync();
        var counts = await Data.DbAsync(async db =>
            (await db.SleepRecords.CountAsync(), await db.ForumComments.CountAsync(), await db.AssistantQueries.CountAsync()));

        var result = await RunAsync(reset: true, now);

        Assert.False(result.Seeded);
        Assert.Contains(result.Lines, l => l.Contains("forum comment"));
        Assert.Equal(demoBefore, await Data.DbAsync(db => db.Users.Where(u => u.NormalizedEmail!.EndsWith(DemoDomain))
            .OrderBy(u => u.Id).Select(u => u.Id).ToListAsync()));
        Assert.Equal(othersBefore, await OthersSnapshotAsync());
        Assert.Equal(counts, await Data.DbAsync(async db =>
            (await db.SleepRecords.CountAsync(), await db.ForumComments.CountAsync(), await db.AssistantQueries.CountAsync())));
    }

    private static DateOnly PolandTimeToday() => Nurtura.Core.PolandTime.Today;

    private static DateOnly DemoTimeToday(DateTime utc) =>
        DateOnly.FromDateTime(Nurtura.Core.PolandTime.ToLocal(utc));
}
