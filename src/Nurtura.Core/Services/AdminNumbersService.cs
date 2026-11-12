using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The admin home: counts only, for the last 30 days and all time. No query here selects a name or any text: not
// from accounts, appointments (no concern text), the forum or the Assistant (which stores no search text anyway).
// For the Admin role only; the caller checks the role.
public class AdminNumbersService
{
    public const int WindowDays = 30;

    private readonly INurturaDbContext _db;

    public AdminNumbersService(INurturaDbContext db)
    {
        _db = db;
    }

    public async Task<AdminNumbers> GetAsync(CancellationToken cancellationToken = default)
    {
        // Statuses must be current before they are counted.
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        // Timestamps: the last 30 days to the minute. AssistantQuery keeps only a date: 30 calendar days with today.
        var since = DateTime.UtcNow.AddDays(-WindowDays);
        var sinceDate = PolandTime.Today.AddDays(-(WindowDays - 1));

        return new AdminNumbers(
            await PeriodAsync(null, null, cancellationToken),
            await PeriodAsync(since, sinceDate, cancellationToken));
    }

    // since and sinceDate are null for all time.
    private async Task<NumbersPeriod> PeriodAsync(DateTime? since, DateOnly? sinceDate, CancellationToken cancellationToken)
    {
        var mothers = _db.MotherProfiles.AsQueryable();
        var professionals = _db.ProfessionalProfiles.AsQueryable();
        var appointments = _db.Appointments.AsQueryable();
        var posts = _db.ForumPosts.AsQueryable();
        var comments = _db.ForumComments.AsQueryable();
        var postReports = _db.PostReports.Where(r => r.Status == ReportStatus.Open);
        var commentReports = _db.CommentReports.Where(r => r.Status == ReportStatus.Open);
        var searches = _db.AssistantQueries.AsQueryable();
        if (since is { } from)
        {
            mothers = mothers.Where(m => m.CreatedAt >= from);
            professionals = professionals.Where(p => p.CreatedAt >= from);
            appointments = appointments.Where(a => a.CreatedAt >= from);
            posts = posts.Where(p => p.CreatedAt >= from);
            comments = comments.Where(c => c.CreatedAt >= from);
            postReports = postReports.Where(r => r.CreatedAt >= from);
            commentReports = commentReports.Where(r => r.CreatedAt >= from);
        }

        if (sinceDate is { } fromDate)
        {
            searches = searches.Where(q => q.Date >= fromDate);
        }

        var byStatus = await appointments
            .GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var byTopic = await appointments
            .GroupBy(a => a.Topic)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var suggested = await searches
            .Where(q => q.SuggestedTopic != null)
            .GroupBy(q => q.SuggestedTopic!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return new NumbersPeriod(
            Mothers: await mothers.CountAsync(cancellationToken),
            ProfessionalsApproved: await professionals.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Approved && p.IsActive, cancellationToken),
            ProfessionalsPending: await professionals.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Pending, cancellationToken),
            ProfessionalsDeactivated: await professionals.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Approved && !p.IsActive, cancellationToken),
            AppointmentsByStatus: Enum.GetValues<AppointmentStatus>().ToDictionary(s => s, s => byStatus.GetValueOrDefault(s)),
            AppointmentsByTopic: Enum.GetValues<Topic>().ToDictionary(t => t, t => byTopic.GetValueOrDefault(t)),
            ForumPosts: await posts.CountAsync(cancellationToken),
            ForumComments: await comments.CountAsync(cancellationToken),
            OpenReports: await postReports.CountAsync(cancellationToken) + await commentReports.CountAsync(cancellationToken),
            Searches: await searches.CountAsync(cancellationToken),
            SearchesMatched: await searches.CountAsync(q => q.Matched, cancellationToken),
            UrgentShown: await searches.CountAsync(q => q.UrgentShown, cancellationToken),
            SuggestedTopics: Enum.GetValues<Topic>().ToDictionary(t => t, t => suggested.GetValueOrDefault(t)));
    }
}

public sealed record AdminNumbers(NumbersPeriod AllTime, NumbersPeriod LastDays);

// Counts for one period. Professionals in the 30-day period are those who signed up in it, by their current state;
// appointments are those requested in it.
public sealed record NumbersPeriod(
    int Mothers,
    int ProfessionalsApproved,
    int ProfessionalsPending,
    int ProfessionalsDeactivated,
    IReadOnlyDictionary<AppointmentStatus, int> AppointmentsByStatus,
    IReadOnlyDictionary<Topic, int> AppointmentsByTopic,
    int ForumPosts,
    int ForumComments,
    int OpenReports,
    int Searches,
    int SearchesMatched,
    int UrgentShown,
    IReadOnlyDictionary<Topic, int> SuggestedTopics)
{
    // Share of searches that matched, in percent with one decimal; null when there were no searches.
    public double? MatchedPercent => Searches == 0 ? null : Math.Round(100.0 * SearchesMatched / Searches, 1);
}
