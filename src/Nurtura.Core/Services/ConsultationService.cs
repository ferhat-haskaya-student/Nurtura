using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The consultation page and the closing note. Every method first finds the room through ChatService, which
// returns it only to the mother of the appointment or the approved professional of its slot, and only for a
// confirmed or completed appointment. Anyone else (another mother, another professional, the admin) gets
// nothing. Only an ended session (end of the hour or End session, worked out from the server clock) has a
// consultation page. Only the professional writes the note, and only she can edit it, for 24 hours.
public class ConsultationService
{
    private readonly INurturaDbContext _db;
    private readonly ChatService _chatService;

    public ConsultationService(INurturaDbContext db, ChatService chatService)
    {
        _db = db;
        _chatService = chatService;
    }

    // Null when the user is not one of its two people, or the session has not ended.
    public async Task<Consultation?> GetAsync(string userId, int appointmentId, CancellationToken cancellationToken = default)
    {
        var room = await _chatService.GetRoomAsync(userId, appointmentId, cancellationToken);
        if (room is not { Info.State: RoomState.Closed })
        {
            return null;
        }

        var details = await (
                from a in _db.Appointments
                where a.Id == appointmentId
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                join p in _db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
                select new { a.Status, a.ConcernText, p.ProfessionalType })
            .SingleAsync(cancellationToken);
        var note = await _db.ConsultationNotes
            .AsNoTracking()
            .SingleOrDefaultAsync(n => n.AppointmentId == appointmentId, cancellationToken);

        var isProfessional = room.Info.Me == AppointmentParty.Professional;
        var utcNow = DateTime.UtcNow;
        return new Consultation(
            room.Info,
            room.Messages,
            details.ProfessionalType,
            details.ConcernText,
            note is null
                ? null
                : new ClosingNote(note.Text, PolandTime.ToLocal(note.CreatedAt), PolandTime.ToLocal(note.CreatedAt + ConsultationNote.EditWindow)),
            isProfessional && note is null && details.Status == AppointmentStatus.Confirmed,
            isProfessional && note is not null && note.CreatedAt + ConsultationNote.EditWindow > utcNow);
    }

    // The professional writes the note (first save) or edits it (within 24 hours of the first save).
    // Text is normalized like the chat ("\r\n" becomes "\n", trimmed) and must be 1 to 2000 characters.
    // First save, in one transaction: the note, Confirmed -> Completed, and one notification to the mother.
    // Edits notify nobody.
    public async Task<NoteSaveResult> SaveNoteAsync(
        string userId,
        int appointmentId,
        string? text,
        CancellationToken cancellationToken = default)
    {
        var normalized = text?.ReplaceLineEndings("\n").Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > ConsultationNote.TextMaxLength)
        {
            return NoteSaveResult.NotAllowed;
        }

        // Her own appointment as its professional, and the session has ended. An ended session never opens again,
        // so this check stays true for the write below.
        var room = await _chatService.GetRoomAsync(userId, appointmentId, cancellationToken);
        if (room is not { Info.State: RoomState.Closed, Info.Me: AppointmentParty.Professional })
        {
            return NoteSaveResult.NotAllowed;
        }

        var utcNow = DateTime.UtcNow;
        var exists = await _db.ConsultationNotes.AnyAsync(n => n.AppointmentId == appointmentId, cancellationToken);
        if (exists)
        {
            // Edit: only within 24 hours of the first save; the conditional UPDATE makes a late save change nothing.
            var editableSince = utcNow - ConsultationNote.EditWindow;
            var changed = await _db.ConsultationNotes
                .Where(n => n.AppointmentId == appointmentId && n.CreatedAt > editableSince)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(n => n.Text, normalized)
                        .SetProperty(n => n.UpdatedAt, utcNow),
                    cancellationToken);
            return changed == 1 ? NoteSaveResult.Updated : NoteSaveResult.NotAllowed;
        }

        var motherUserId = await (
                from a in _db.Appointments
                where a.Id == appointmentId
                join m in _db.MotherProfiles on a.MotherProfileId equals m.Id
                select m.UserId)
            .SingleAsync(cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var completed = await _db.Appointments
            .Where(a => a.Id == appointmentId && a.Status == AppointmentStatus.Confirmed)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(a => a.Status, AppointmentStatus.Completed)
                    .SetProperty(a => a.UpdatedAt, utcNow),
                cancellationToken);
        if (completed != 1)
        {
            return NoteSaveResult.NotAllowed;
        }

        _db.ConsultationNotes.Add(new ConsultationNote
        {
            AppointmentId = appointmentId,
            Text = normalized,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });
        NotificationWriter.Add(
            _db, motherUserId, NotificationType.ClosingNoteWritten, appointmentId, room.Info.ProfessionalName,
            room.Info.Date, room.Info.Hour);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another first save got there first (one note per appointment). Nothing of this one is kept.
            return NoteSaveResult.NotAllowed;
        }

        await transaction.CommitAsync(cancellationToken);
        return NoteSaveResult.Created;
    }
}
