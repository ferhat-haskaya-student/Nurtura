using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// A mother deletes her own account. Every method takes the logged-in user's id and works only on her own rows;
// the caller checks her password (or email) first.
//
// What goes, in one transaction:
// - By hand: a notification to each professional whose appointment with her had not ended; her forum comments
//   (with them, by cascade, their reports and the comment notifications they caused); posts left empty by that;
//   her forum posts (deleted, or cleared and closed when others' comments remain); last, the Identity user.
// - By cascade from the Identity user: her logins (Google), roles, claims, tokens, her notifications, and her
//   mother profile; from the profile: babies with sleeps and feeds, check-ins with tags, concerns, support types,
//   appointments with chat messages, closing notes and their notifications, saved guides, saved and hidden posts,
//   and the reports she wrote.
public class AccountDeletionService
{
    private readonly INurturaDbContext _db;

    public AccountDeletionService(INurturaDbContext db)
    {
        _db = db;
    }

    // For the My account page. Null when the user has no mother profile.
    public async Task<MotherAccount?> GetMotherAccountAsync(string userId, CancellationToken cancellationToken = default) =>
        await (
                from p in _db.MotherProfiles
                where p.UserId == userId
                join u in _db.Users on p.UserId equals u.Id
                select new MotherAccount(p.DisplayName, u.Email ?? string.Empty, u.PasswordHash != null))
            .SingleOrDefaultAsync(cancellationToken);

    // Deletes everything of hers. False when the user has no mother profile.
    public async Task<bool> DeleteMotherAsync(string userId, CancellationToken cancellationToken = default)
    {
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var me = await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => new { p.Id, p.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);
        if (me is null)
        {
            return false;
        }

        // Requested or confirmed appointments whose session has not ended (including one in progress): the
        // professional is told, without her name. The appointments themselves go with her profile.
        var now = PolandTime.Now;
        var active = await (
                from a in _db.Appointments
                where a.MotherProfileId == me.Id
                    && (a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Confirmed)
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                join p in _db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
                select new { p.UserId, s.Date, s.StartHour, a.SessionEndedAt })
            .ToListAsync(cancellationToken);
        foreach (var appointment in active.Where(a =>
                     ConsultationRoom.StateOf(a.Date, a.StartHour, a.SessionEndedAt, now) != RoomState.Closed))
        {
            NotificationWriter.AddMotherDeletedAccount(_db, appointment.UserId, appointment.Date, appointment.StartHour);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Posts she commented on, for the old notifications below and the last-comment rule.
        var commentedPostIds = await _db.ForumComments
            .Where(c => c.AuthorProfileId == me.Id)
            .Select(c => c.ForumPostId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Comment notifications written before they linked to their comment: matched by post and her name.
        await _db.Notifications
            .Where(n => n.Type == NotificationType.CommentOnYourPost && n.ForumCommentId == null
                && n.ForumPostId != null && commentedPostIds.Contains(n.ForumPostId.Value) && n.OtherName == me.DisplayName)
            .ExecuteDeleteAsync(cancellationToken);

        // Her comments; their reports and linked notifications go by cascade.
        await _db.ForumComments
            .Where(c => c.AuthorProfileId == me.Id)
            .ExecuteDeleteAsync(cancellationToken);

        // A post deleted by its author or removed by the admin disappears with its last comment.
        await _db.ForumPosts
            .Where(p => commentedPostIds.Contains(p.Id) && (p.DeletedByAuthorAt != null || p.RemovedByModeratorAt != null)
                && !_db.ForumComments.Any(c => c.ForumPostId == p.Id))
            .ExecuteDeleteAsync(cancellationToken);

        // Her posts: without (others') comments, deleted; with them, cleared and closed like an author delete, with
        // no author.
        await _db.ForumPosts
            .Where(p => p.AuthorProfileId == me.Id && !_db.ForumComments.Any(c => c.ForumPostId == p.Id))
            .ExecuteDeleteAsync(cancellationToken);
        var utcNow = DateTime.UtcNow;
        await _db.ForumPosts
            .Where(p => p.AuthorProfileId == me.Id && p.DeletedByAuthorAt == null && p.RemovedByModeratorAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.DeletedByAuthorAt, utcNow), cancellationToken);
        await _db.ForumPosts
            .Where(p => p.AuthorProfileId == me.Id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(p => p.Title, string.Empty)
                    .SetProperty(p => p.Body, string.Empty)
                    .SetProperty(p => p.AuthorProfileId, (int?)null),
                cancellationToken);

        // Last, the Identity user; everything else of hers goes by cascade.
        await _db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}

public sealed record MotherAccount(string DisplayName, string Email, bool HasPassword);
