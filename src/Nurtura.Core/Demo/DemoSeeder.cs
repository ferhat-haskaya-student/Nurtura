using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Core.Demo;

public sealed record DemoSeedResult(bool Seeded, IReadOnlyList<string> Lines);

// Writes the demo data (design doc §4 "Demo data"). Only in Development, only when F asks for it with a
// command-line flag, never on the test database (the guard in Program.cs). One transaction each. --seed-demo changes
// nothing if a demo account exists; --reset-demo replaces the demo data. The test accounts are never touched.
public static class DemoSeeder
{
    public const string Flag = "--seed-demo";

    public const string ResetFlag = "--reset-demo";

    public const string TestDatabaseName = "nurtura_test";

    // Null when seeding is allowed; otherwise why not.
    public static string? RefusalReason(bool isDevelopment, string? databaseName)
    {
        if (!isDevelopment)
        {
            return "The demo data is only seeded in Development. Nothing was changed.";
        }

        if (string.Equals(databaseName, TestDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            return $"The demo data is never seeded on the '{TestDatabaseName}' database. Nothing was changed.";
        }

        return null;
    }

    // --seed-demo: changes nothing if any demo account exists, also one of an earlier run with other names.
    public static async Task<DemoSeedResult> SeedAsync(
        UserManager<ApplicationUser> userManager,
        INurturaDbContext db,
        string password,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var existing = await DemoUsers(db).Select(u => u.Email!).ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return new DemoSeedResult(false,
                ["Demo accounts already exist, nothing changed: " + string.Join(", ", existing.Order())]);
        }

        var data = DemoDataGenerator.Generate(utcNow);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await WriteAsync(userManager, db, password, data, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DemoSeedResult(true, Summary(data));
    }

    // --reset-demo: removes every @demo.nurtura account and everything that belongs to it, then seeds again, in one
    // transaction. Refused, with nothing changed, while any other account is linked to the demo data (an
    // appointment, a comment, a report, a saved or hidden post across the two), so no other account's data is ever
    // touched.
    public static async Task<DemoSeedResult> ResetAsync(
        UserManager<ApplicationUser> userManager,
        INurturaDbContext db,
        string password,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var lines = new List<string>();

        var users = await DemoUsers(db).Select(u => new { u.Id, u.Email }).ToListAsync(cancellationToken);
        var userIds = users.Select(u => u.Id).ToList();
        var mothers = await db.MotherProfiles
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new { p.Id, p.UserId, p.CreatedAt })
            .ToListAsync(cancellationToken);
        var motherIds = mothers.Select(m => m.Id).ToList();
        var professionalIds = await db.ProfessionalProfiles
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var links = await LinksAsync(db, motherIds, professionalIds, cancellationToken);
        if (links.Count > 0)
        {
            return new DemoSeedResult(false,
            [
                "Other accounts are linked to the demo data, nothing changed. Remove these links in the app first:",
                .. links.Select(l => "  " + l)
            ]);
        }

        // The earlier run's Assistant searches, found before its accounts go (the date comes from them).
        var demoQueryIds = await EarlierAssistantQueryIdsAsync(db, mothers.Select(m => m.CreatedAt), cancellationToken);

        // Comments and posts first: they would only lose their author when a mother goes. Their reports, saved and
        // hidden rows and notifications go with them by cascade.
        await db.ForumComments
            .Where(c => (c.AuthorProfileId != null && motherIds.Contains(c.AuthorProfileId.Value))
                || db.ForumPosts.Any(p => p.Id == c.ForumPostId
                    && p.AuthorProfileId != null && motherIds.Contains(p.AuthorProfileId.Value)))
            .ExecuteDeleteAsync(cancellationToken);
        await db.ForumPosts
            .Where(p => p.AuthorProfileId != null && motherIds.Contains(p.AuthorProfileId.Value))
            .ExecuteDeleteAsync(cancellationToken);

        // The mothers' users, then the rest. Everything else goes by cascade, as in AccountDeletionService: profiles,
        // babies with sleeps and feeds, check-ins, appointments with chats, notes and notifications, then the
        // professionals with their slots, topics and languages (a slot cannot go while an appointment uses it).
        var motherUserIds = mothers.Select(m => m.UserId).ToList();
        await db.Users.Where(u => motherUserIds.Contains(u.Id)).ExecuteDeleteAsync(cancellationToken);
        await db.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync(cancellationToken);

        if (users.Count > 0)
        {
            lines.Add($"Removed {users.Count} demo accounts: " + string.Join(", ", users.Select(u => u.Email).Order()));
            if (demoQueryIds.Count > 0)
            {
                await db.AssistantQueries.Where(q => demoQueryIds.Contains(q.Id)).ExecuteDeleteAsync(cancellationToken);
                lines.Add($"Removed the earlier run's {demoQueryIds.Count} Assistant searches; all others are kept.");
            }
            else
            {
                lines.Add("The earlier run's Assistant searches were not found, so no Assistant search was removed.");
            }
        }

        var data = DemoDataGenerator.Generate(utcNow);
        await WriteAsync(userManager, db, password, data, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DemoSeedResult(true, [.. lines, .. Summary(data)]);
    }

    // Every account on the demo domain, whatever its name.
    private static IQueryable<ApplicationUser> DemoUsers(INurturaDbContext db)
    {
        var domain = DemoAccounts.Domain.ToUpperInvariant();
        return db.Users.Where(u => u.NormalizedEmail != null && u.NormalizedEmail.EndsWith(domain));
    }

    // Links between the demo accounts and any other account, which a reset must not break.
    private static async Task<List<string>> LinksAsync(
        INurturaDbContext db, List<int> motherIds, List<int> professionalIds, CancellationToken cancellationToken)
    {
        var links = new List<string>();
        void Add(int count, string what)
        {
            if (count > 0)
            {
                links.Add($"{count} {what} between a demo account and another account");
            }
        }

        Add(await (
                from a in db.Appointments
                join s in db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                where motherIds.Contains(a.MotherProfileId) != professionalIds.Contains(s.ProfessionalProfileId)
                select a.Id).CountAsync(cancellationToken),
            "appointment(s)");

        Add(await (
                from c in db.ForumComments
                join p in db.ForumPosts on c.ForumPostId equals p.Id
                where (c.AuthorProfileId != null && motherIds.Contains(c.AuthorProfileId.Value))
                    != (p.AuthorProfileId != null && motherIds.Contains(p.AuthorProfileId.Value))
                select c.Id).CountAsync(cancellationToken),
            "forum comment(s)");

        Add(await (
                    from r in db.PostReports
                    join p in db.ForumPosts on r.ForumPostId equals p.Id
                    where motherIds.Contains(r.ReporterProfileId)
                        != (p.AuthorProfileId != null && motherIds.Contains(p.AuthorProfileId.Value))
                    select r.Id).CountAsync(cancellationToken)
                + await (
                    from r in db.CommentReports
                    join c in db.ForumComments on r.ForumCommentId equals c.Id
                    where motherIds.Contains(r.ReporterProfileId)
                        != (c.AuthorProfileId != null && motherIds.Contains(c.AuthorProfileId.Value))
                    select r.Id).CountAsync(cancellationToken),
            "forum report(s)");

        Add(await (
                    from s in db.SavedPosts
                    join p in db.ForumPosts on s.ForumPostId equals p.Id
                    where motherIds.Contains(s.MotherProfileId)
                        != (p.AuthorProfileId != null && motherIds.Contains(p.AuthorProfileId.Value))
                    select s.Id).CountAsync(cancellationToken)
                + await (
                    from h in db.HiddenPosts
                    join p in db.ForumPosts on h.ForumPostId equals p.Id
                    where motherIds.Contains(h.MotherProfileId)
                        != (p.AuthorProfileId != null && motherIds.Contains(p.AuthorProfileId.Value))
                    select h.Id).CountAsync(cancellationToken),
            "saved or hidden post(s)");

        return links;
    }

    // An AssistantQuery has no owner, so an earlier run's rows are found by what they are: the 40 rows that run wrote
    // in one insert, so 40 consecutive ids, equal in order to DemoDataGenerator.AssistantQueries for its date. The
    // run's date comes from the demo mothers' sign-up (06:00 on its first day, 3 months before it). Empty when no such
    // run of rows is found; nobody else's searches are ever matched by value alone.
    private static async Task<List<int>> EarlierAssistantQueryIdsAsync(
        INurturaDbContext db, IEnumerable<DateTime> motherCreatedAt, CancellationToken cancellationToken)
    {
        var signedUp = motherCreatedAt.ToList();
        if (signedUp.Count == 0)
        {
            return [];
        }

        var firstDay = DateOnly.FromDateTime(PolandTime.ToLocal(signedUp.Min()));
        var rows = await db.AssistantQueries
            .OrderBy(q => q.Id)
            .Select(q => new { q.Id, q.Date, q.Matched, q.SuggestedTopic, q.UrgentShown })
            .ToListAsync(cancellationToken);

        // At a month end several days lead back to the same first day 3 months earlier; each is tried.
        for (var today = firstDay.AddDays(95); today >= firstDay.AddDays(85); today = today.AddDays(-1))
        {
            if (today.AddMonths(-3) != firstDay)
            {
                continue;
            }

            var expected = DemoDataGenerator.AssistantQueries(today);
            for (var i = rows.Count - expected.Count; i >= 0; i--)
            {
                var match = true;
                for (var k = 0; k < expected.Count && match; k++)
                {
                    var (row, want) = (rows[i + k], expected[k]);
                    match = row.Id == rows[i].Id + k && row.Date == want.Date && row.Matched == want.Matched
                        && row.SuggestedTopic == want.SuggestedTopic && row.UrgentShown == want.UrgentShown;
                }

                if (match)
                {
                    return rows.Skip(i).Take(expected.Count).Select(r => r.Id).ToList();
                }
            }
        }

        return [];
    }

    // Writes one set of demo data inside the caller's transaction.
    private static async Task WriteAsync(
        UserManager<ApplicationUser> userManager,
        INurturaDbContext db,
        string password,
        DemoData data,
        CancellationToken cancellationToken)
    {
        // Accounts and profiles.
        var userIds = new Dictionary<string, string>();
        foreach (var mother in data.Mothers)
        {
            userIds[mother.Email] = await CreateUserAsync(userManager, mother.Email, password, Roles.Mother);
            mother.Profile.UserId = userIds[mother.Email];
            db.MotherProfiles.Add(mother.Profile);
        }

        foreach (var professional in data.Professionals)
        {
            userIds[professional.Email] =
                await CreateUserAsync(userManager, professional.Email, password, Roles.Professional);
            professional.Profile.UserId = userIds[professional.Email];
            db.ProfessionalProfiles.Add(professional.Profile);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Onboarding answers, babies, topics, languages and open hours.
        foreach (var mother in data.Mothers)
        {
            var profileId = mother.Profile.Id;
            db.MotherConcerns.AddRange(mother.Concerns.Select(c => new MotherConcern
            {
                MotherProfileId = profileId, Concern = c
            }));
            db.MotherSupportTypes.AddRange(mother.SupportTypes.Select(s => new MotherSupportType
            {
                MotherProfileId = profileId, SupportType = s
            }));
            mother.Baby.MotherProfileId = profileId;
            db.Babies.Add(mother.Baby);
        }

        foreach (var professional in data.Professionals)
        {
            var profileId = professional.Profile.Id;
            db.ProfessionalCategories.AddRange(professional.Topics.Select(t => new ProfessionalCategory
            {
                ProfessionalProfileId = profileId, Category = t
            }));
            db.ProfessionalLanguages.AddRange(professional.Languages.Select(l => new ProfessionalLanguage
            {
                ProfessionalProfileId = profileId, Language = l
            }));
            foreach (var slot in professional.Slots)
            {
                slot.ProfessionalProfileId = profileId;
            }

            db.AvailabilitySlots.AddRange(professional.Slots);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Logs, check-ins, appointments and forum posts.
        foreach (var mother in data.Mothers)
        {
            foreach (var sleep in mother.Sleeps)
            {
                sleep.BabyId = mother.Baby.Id;
            }

            foreach (var feed in mother.Feeds)
            {
                feed.BabyId = mother.Baby.Id;
            }

            foreach (var checkIn in mother.CheckIns)
            {
                checkIn.MotherProfileId = mother.Profile.Id;
            }

            db.SleepRecords.AddRange(mother.Sleeps);
            db.FeedingRecords.AddRange(mother.Feeds);
            db.DailyCheckIns.AddRange(mother.CheckIns);
        }

        foreach (var appointment in data.Appointments)
        {
            appointment.Appointment.MotherProfileId = appointment.Mother.Profile.Id;
            appointment.Appointment.AvailabilitySlotId = appointment.Slot.Id;
            appointment.Appointment.BabyId = appointment.AboutBaby ? appointment.Mother.Baby.Id : null;
            db.Appointments.Add(appointment.Appointment);
        }

        foreach (var post in data.Posts)
        {
            post.Post.AuthorProfileId = post.Author.Profile.Id;
            db.ForumPosts.Add(post.Post);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Chats, closing notes, comments and saved posts.
        foreach (var appointment in data.Appointments)
        {
            foreach (var message in appointment.Messages)
            {
                message.AppointmentId = appointment.Appointment.Id;
            }

            db.ChatMessages.AddRange(appointment.Messages);
            if (appointment.Note is { } note)
            {
                note.AppointmentId = appointment.Appointment.Id;
                db.ConsultationNotes.Add(note);
            }
        }

        foreach (var post in data.Posts)
        {
            foreach (var comment in post.Comments)
            {
                comment.Comment.ForumPostId = post.Post.Id;
                comment.Comment.AuthorProfileId = comment.Author.Profile.Id;
                db.ForumComments.Add(comment.Comment);
            }
        }

        foreach (var saved in data.SavedPosts)
        {
            saved.Saved.MotherProfileId = saved.Mother.Profile.Id;
            saved.Saved.ForumPostId = saved.Post.Post.Id;
            db.SavedPosts.Add(saved.Saved);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Reports, notifications and the Assistant's counts.
        foreach (var report in data.CommentReports)
        {
            report.Report.ReporterProfileId = report.Reporter.Profile.Id;
            report.Report.ForumCommentId = report.Comment.Comment.Id;
            db.CommentReports.Add(report.Report);
        }

        foreach (var item in data.Notifications)
        {
            item.Notification.UserId = userIds[item.RecipientEmail];
            item.Notification.AppointmentId = item.Appointment?.Appointment.Id;
            item.Notification.ForumPostId = item.Post?.Post.Id;
            item.Notification.ForumCommentId = item.Comment?.Comment.Id;
            db.Notifications.Add(item.Notification);
        }

        db.AssistantQueries.AddRange(data.AssistantQueries);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<string> CreateUserAsync(
        UserManager<ApplicationUser> userManager, string email, string password, string role)
    {
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        Check(await userManager.CreateAsync(user, password));
        Check(await userManager.AddToRoleAsync(user, role));
        return user.Id;
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Demo seeding failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    // What was added: counts and the account emails (the password is Seed:TestPassword, never printed).
    private static List<string> Summary(DemoData data)
    {
        var lines = new List<string>
        {
            $"Demo data seeded for {data.FirstDay:dd-MM-yyyy} to {data.Today:dd-MM-yyyy}. Password: Seed:TestPassword."
        };
        foreach (var mother in data.Mothers)
        {
            lines.Add($"  {mother.Email} ({mother.Profile.DisplayName}, baby {mother.Baby.Name}): " +
                      $"{mother.Sleeps.Count} sleeps, {mother.Feeds.Count} feeds, {mother.CheckIns.Count} check-ins");
        }

        foreach (var professional in data.Professionals)
        {
            lines.Add($"  {professional.Email} ({professional.Profile.DisplayName}, " +
                      $"{professional.Profile.ApprovalStatus}): {professional.Slots.Count} slots");
        }

        lines.Add($"  {data.Appointments.Count} appointments, {data.Posts.Count} forum posts, " +
                  $"{data.Posts.Sum(p => p.Comments.Count)} comments, {data.Notifications.Count} notifications, " +
                  $"{data.AssistantQueries.Count} Assistant searches");
        return lines;
    }
}
