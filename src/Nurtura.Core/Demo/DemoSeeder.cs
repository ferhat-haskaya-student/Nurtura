using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;

namespace Nurtura.Core.Demo;

public sealed record DemoSeedResult(bool Seeded, IReadOnlyList<string> Lines);

// Writes the demo data (design doc §4 "Demo data"). Only in Development, only when F asks for it with the
// command-line flag, never on the test database. One transaction; changes nothing if a demo account exists.
// The test accounts are not touched.
public static class DemoSeeder
{
    public const string Flag = "--seed-demo";

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

    public static async Task<DemoSeedResult> SeedAsync(
        UserManager<ApplicationUser> userManager,
        INurturaDbContext db,
        string password,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var data = DemoDataGenerator.Generate(utcNow);
        var emails = data.Emails.ToList();
        var existing = await db.Users
            .Where(u => u.Email != null && emails.Contains(u.Email))
            .Select(u => u.Email!)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return new DemoSeedResult(false,
                ["Demo accounts already exist, nothing changed: " + string.Join(", ", existing)]);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

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
        await transaction.CommitAsync(cancellationToken);

        return new DemoSeedResult(true, Summary(data));
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
