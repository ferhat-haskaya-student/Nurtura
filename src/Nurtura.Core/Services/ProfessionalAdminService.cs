using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The admin's Professionals page: the approved professionals, deactivate / reactivate, and the topics each one
// covers. For the Admin role only; the caller checks the role. The admin sees each appointment's date, hour,
// topic and status only: never mothers' names, concern text, chats or notes.
public class ProfessionalAdminService
{
    // The sender's name on the cancelled notification when a deactivation cancels an appointment. A product
    // name, the same in both languages.
    public const string NurturaName = "Nurtura";

    private readonly INurturaDbContext _db;
    private readonly ProfessionalApprovalService _approvalService;

    public ProfessionalAdminService(INurturaDbContext db, ProfessionalApprovalService approvalService)
    {
        _db = db;
        _approvalService = approvalService;
    }

    // Pending first (the existing approval list), then approved active, then deactivated, each by name.
    public async Task<ProfessionalsOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);
        var pending = await _approvalService.GetPendingAsync(cancellationToken);
        var (cutoffDate, cutoffHour) = ConsultationRoom.OpeningCutoff(PolandTime.Now);

        var approved = await _db.ProfessionalProfiles
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .OrderBy(p => p.DisplayName)
            .Join(_db.Users, p => p.UserId, u => u.Id, (p, u) => new ProfessionalListItem(
                p.Id,
                p.DisplayName,
                u.Email,
                p.ProfessionalType,
                _db.ProfessionalCategories
                    .Where(c => c.ProfessionalProfileId == p.Id)
                    .OrderBy(c => c.Category)
                    .Select(c => c.Category)
                    .ToList(),
                _db.ProfessionalLanguages
                    .Where(l => l.ProfessionalProfileId == p.Id)
                    .OrderBy(l => l.Id)
                    .Select(l => l.Language)
                    .ToList(),
                p.IsActive,
                _db.Appointments.Count(a =>
                    (a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Confirmed)
                    && _db.AvailabilitySlots.Any(s => s.Id == a.AvailabilitySlotId && s.ProfessionalProfileId == p.Id
                        && (s.Date > cutoffDate || (s.Date == cutoffDate && s.StartHour > cutoffHour))))))
            .ToListAsync(cancellationToken);

        return new ProfessionalsOverview(
            pending,
            approved.Where(p => p.IsActive).ToList(),
            approved.Where(p => !p.IsActive).ToList());
    }

    // What deactivating her would cancel: her requested and confirmed appointments whose chat room has not opened
    // yet, soonest first. HasOpenSession: one of her sessions is open now, so deactivation is refused.
    // Null when she is not an approved, active professional.
    public async Task<DeactivationInfo?> GetDeactivationAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);
        var name = await _db.ProfessionalProfiles
            .Where(p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Approved && p.IsActive)
            .Select(p => p.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        if (name is null)
        {
            return null;
        }

        var upcoming = (await UpcomingAsync(profileId, cancellationToken))
            .Select(a => new UpcomingAppointment(a.Date, a.StartHour, a.Topic, a.Status))
            .ToList();
        return new DeactivationInfo(profileId, name, upcoming, await HasOpenSessionAsync(profileId, cancellationToken));
    }

    // In one transaction: she becomes inactive; her requested and confirmed appointments whose room has not opened
    // are cancelled by Nurtura and each mother gets the cancelled notification from "Nurtura"; all her future open
    // hours are closed (on reactivation she opens new ones herself). Refused while one of her sessions is open,
    // because a cancelled appointment has no chat, so the mother would lose that conversation.
    public async Task<DeactivateResult> DeactivateAsync(int profileId, CancellationToken cancellationToken = default)
    {
        await AppointmentExpiry.ExpireOverdueAsync(_db, cancellationToken);
        if (!await _db.ProfessionalProfiles.AnyAsync(
                p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Approved && p.IsActive, cancellationToken))
        {
            return new DeactivateResult(DeactivateStatus.NotFound, string.Empty, 0);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (await HasOpenSessionAsync(profileId, cancellationToken))
        {
            var busyName = await NameAsync(profileId, cancellationToken);
            return new DeactivateResult(DeactivateStatus.SessionInProgress, busyName, 0);
        }

        var deactivated = await _db.ProfessionalProfiles
            .Where(p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Approved && p.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.IsActive, false), cancellationToken);
        if (deactivated != 1)
        {
            return new DeactivateResult(DeactivateStatus.NotFound, string.Empty, 0);
        }

        var utcNow = DateTime.UtcNow;
        var (cutoffDate, cutoffHour) = ConsultationRoom.OpeningCutoff(PolandTime.Now);
        var cancelled = 0;
        foreach (var appointment in await UpcomingAsync(profileId, cancellationToken))
        {
            // Each one only if it is still requested or confirmed and its room has still not opened.
            var changed = await _db.Appointments
                .Where(a => a.Id == appointment.Id
                    && (a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Confirmed)
                    && _db.AvailabilitySlots.Any(s => s.Id == a.AvailabilitySlotId
                        && (s.Date > cutoffDate || (s.Date == cutoffDate && s.StartHour > cutoffHour))))
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(a => a.Status, AppointmentStatus.Cancelled)
                        .SetProperty(a => a.CancelledBy, AppointmentParty.Nurtura)
                        .SetProperty(a => a.UpdatedAt, utcNow),
                    cancellationToken);
            if (changed == 1)
            {
                NotificationWriter.Add(
                    _db, appointment.MotherUserId, NotificationType.CancelledByProfessional, appointment.Id,
                    NurturaName, appointment.Date, appointment.StartHour);
                cancelled++;
            }
        }

        // Her open hours that have not started: closed, not deleted (a cancelled appointment keeps pointing to its slot).
        var today = PolandTime.Today;
        var hourNow = PolandTime.Now.Hour;
        await _db.AvailabilitySlots
            .Where(s => s.ProfessionalProfileId == profileId && s.IsOpen
                && (s.Date > today || (s.Date == today && s.StartHour > hourNow)))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.IsOpen, false)
                    .SetProperty(s => s.UpdatedAt, utcNow),
                cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeactivateResult(DeactivateStatus.Deactivated, await NameAsync(profileId, cancellationToken), cancelled);
    }

    // Brings back a deactivated, approved professional. Her hours stay closed; she opens new ones herself.
    // Returns her name, or null when she was not a deactivated, approved professional.
    public async Task<string?> ReactivateAsync(int profileId, CancellationToken cancellationToken = default)
    {
        var changed = await _db.ProfessionalProfiles
            .Where(p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Approved && !p.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.IsActive, true), cancellationToken);
        return changed == 1 ? await NameAsync(profileId, cancellationToken) : null;
    }

    // An approved professional's name and topics; null for any other id.
    public async Task<TopicsEdit?> GetTopicsAsync(int profileId, CancellationToken cancellationToken = default)
    {
        var name = await _db.ProfessionalProfiles
            .Where(p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => p.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        if (name is null)
        {
            return null;
        }

        var topics = await _db.ProfessionalCategories
            .Where(c => c.ProfessionalProfileId == profileId)
            .Select(c => c.Category)
            .ToListAsync(cancellationToken);
        return new TopicsEdit(profileId, name, topics);
    }

    // Replaces the topics an approved professional covers: one or more of the six. Existing appointments keep their
    // topic. Returns her name; null when she is not approved or the topics are not valid.
    public async Task<string?> SetTopicsAsync(int profileId, IReadOnlyCollection<Topic> topics, CancellationToken cancellationToken = default)
    {
        var chosen = topics.Distinct().ToList();
        if (chosen.Count == 0 || chosen.Any(t => !Enum.IsDefined(t)))
        {
            return null;
        }

        var name = await _db.ProfessionalProfiles
            .Where(p => p.Id == profileId && p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => p.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        if (name is null)
        {
            return null;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.ProfessionalCategories
            .Where(c => c.ProfessionalProfileId == profileId)
            .ExecuteDeleteAsync(cancellationToken);
        foreach (var topic in chosen)
        {
            _db.ProfessionalCategories.Add(new ProfessionalCategory { ProfessionalProfileId = profileId, Category = topic });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return name;
    }

    // Her requested and confirmed appointments whose room has not opened yet, soonest first.
    private async Task<List<UpcomingRow>> UpcomingAsync(int profileId, CancellationToken cancellationToken)
    {
        var (cutoffDate, cutoffHour) = ConsultationRoom.OpeningCutoff(PolandTime.Now);
        return await (
                from a in _db.Appointments
                where a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Confirmed
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                where s.ProfessionalProfileId == profileId
                    && (s.Date > cutoffDate || (s.Date == cutoffDate && s.StartHour > cutoffHour))
                join m in _db.MotherProfiles on a.MotherProfileId equals m.Id
                orderby s.Date, s.StartHour
                select new UpcomingRow(a.Id, m.UserId, s.Date, s.StartHour, a.Topic, a.Status))
            .ToListAsync(cancellationToken);
    }

    // A confirmed session of hers whose room is open now (from 5 minutes before the start until its end).
    private async Task<bool> HasOpenSessionAsync(int profileId, CancellationToken cancellationToken)
    {
        var now = PolandTime.Now;
        var today = PolandTime.Today;
        var candidates = await (
                from a in _db.Appointments
                where a.Status == AppointmentStatus.Confirmed
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                where s.ProfessionalProfileId == profileId && s.Date >= today.AddDays(-1) && s.Date <= today.AddDays(1)
                select new { s.Date, s.StartHour, a.SessionEndedAt })
            .ToListAsync(cancellationToken);
        return candidates.Any(c => ConsultationRoom.StateOf(c.Date, c.StartHour, c.SessionEndedAt, now) == RoomState.Open);
    }

    private async Task<string> NameAsync(int profileId, CancellationToken cancellationToken) =>
        await _db.ProfessionalProfiles.Where(p => p.Id == profileId).Select(p => p.DisplayName).SingleAsync(cancellationToken);

    private sealed record UpcomingRow(int Id, string MotherUserId, DateOnly Date, int StartHour, Topic Topic, AppointmentStatus Status);
}

// One approved professional on the admin's list. UpcomingCount: requested and confirmed appointments whose room
// has not opened yet.
public sealed record ProfessionalListItem(
    int ProfileId,
    string DisplayName,
    string? Email,
    ProfessionalType ProfessionalType,
    IReadOnlyList<Topic> Topics,
    IReadOnlyList<Language> Languages,
    bool IsActive,
    int UpcomingCount);

public sealed record ProfessionalsOverview(
    IReadOnlyList<PendingProfessional> Pending,
    List<ProfessionalListItem> Active,
    List<ProfessionalListItem> Deactivated);

public sealed record UpcomingAppointment(DateOnly Date, int Hour, Topic Topic, AppointmentStatus Status);

public sealed record DeactivationInfo(int ProfileId, string DisplayName, List<UpcomingAppointment> Upcoming, bool HasOpenSession);

public enum DeactivateStatus
{
    Deactivated,
    SessionInProgress,
    NotFound
}

public sealed record DeactivateResult(DeactivateStatus Status, string DisplayName, int CancelledCount);

public sealed record TopicsEdit(int ProfileId, string DisplayName, IReadOnlyList<Topic> Topics);
