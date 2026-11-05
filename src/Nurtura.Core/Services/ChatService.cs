using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// The consultation chat. Every method takes the logged-in user's id and first finds the appointment through
// her own profile: as its mother, or as the approved professional of its slot. Anyone else (another mother,
// another professional, the admin) gets nothing. Only a confirmed appointment has a room (a completed one
// stays readable). Whether the room is open is always worked out here from the server clock.
public class ChatService
{
    private readonly INurturaDbContext _db;

    public ChatService(INurturaDbContext db)
    {
        _db = db;
    }

    // The room with all its messages, oldest first; null when the user is not one of its two people.
    public async Task<ChatRoom?> GetRoomAsync(string userId, int appointmentId, CancellationToken cancellationToken = default)
    {
        var room = await FindAsync(userId, appointmentId, cancellationToken);
        if (room is null)
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;
        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.AppointmentId == appointmentId)
            .OrderBy(m => m.SentAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        return new ChatRoom(room, messages.Select(m => ToItem(m, room.State, utcNow)).ToList());
    }

    // Text is normalized ("\r\n" becomes "\n", trimmed) and must be 1 to 1000 characters. Only while the room is open.
    public async Task<ChatSendResult?> SendAsync(string userId, int appointmentId, string? text, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(text);
        if (normalized is null)
        {
            return null;
        }

        var room = await FindAsync(userId, appointmentId, cancellationToken);
        if (room is not { State: RoomState.Open })
        {
            return null;
        }

        var message = new ChatMessage
        {
            AppointmentId = appointmentId,
            Sender = room.Me,
            Text = normalized,
            SentAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        return new ChatSendResult(room, ToItem(message, room.State, message.SentAt));
    }

    // Her own message only, within 3 minutes of sending, only while the room is open. No deleting.
    public async Task<ChatSendResult?> EditAsync(
        string userId,
        int appointmentId,
        int messageId,
        string? text,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(text);
        if (normalized is null)
        {
            return null;
        }

        var room = await FindAsync(userId, appointmentId, cancellationToken);
        if (room is not { State: RoomState.Open })
        {
            return null;
        }

        var utcNow = DateTime.UtcNow;
        var editableSince = utcNow - ChatMessage.EditWindow;
        var changed = await _db.ChatMessages
            .Where(m => m.Id == messageId && m.AppointmentId == appointmentId && m.Sender == room.Me && m.SentAt > editableSince)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(m => m.Text, normalized)
                    .SetProperty(m => m.EditedAt, utcNow),
                cancellationToken);
        if (changed != 1)
        {
            return null;
        }

        var message = await _db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == messageId, cancellationToken);
        return new ChatSendResult(room, ToItem(message, room.State, utcNow));
    }

    // End session: either side, only from the start time until the end of the hour, and only once. Closes the
    // room for both. Returns the room as it is now (closed), or null when it was not allowed.
    public async Task<ChatRoomInfo?> EndSessionAsync(string userId, int appointmentId, CancellationToken cancellationToken = default)
    {
        var room = await FindAsync(userId, appointmentId, cancellationToken);
        if (room is not { CanEndSession: true })
        {
            return null;
        }

        // Slots are one hour on the full hour, so "from the start until the end of the hour" is exactly
        // "the slot is today at the current hour". The conditional UPDATE also makes a second End session
        // (from the other side at the same moment) change nothing.
        var now = PolandTime.Now;
        var today = DateOnly.FromDateTime(now);
        var hour = now.Hour;
        var utcNow = DateTime.UtcNow;
        var changed = await _db.Appointments
            .Where(a => a.Id == appointmentId && a.Status == AppointmentStatus.Confirmed && a.SessionEndedAt == null
                && _db.AvailabilitySlots.Any(s => s.Id == a.AvailabilitySlotId && s.Date == today && s.StartHour == hour))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(a => a.SessionEndedAt, utcNow)
                    .SetProperty(a => a.SessionEndedBy, room.Me)
                    .SetProperty(a => a.UpdatedAt, utcNow),
                cancellationToken);

        return changed == 1 ? await FindAsync(userId, appointmentId, cancellationToken) : null;
    }

    private async Task<ChatRoomInfo?> FindAsync(string userId, int appointmentId, CancellationToken cancellationToken)
    {
        var row = await (
                from a in _db.Appointments
                where a.Id == appointmentId && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Completed)
                join s in _db.AvailabilitySlots on a.AvailabilitySlotId equals s.Id
                join p in _db.ProfessionalProfiles on s.ProfessionalProfileId equals p.Id
                join m in _db.MotherProfiles on a.MotherProfileId equals m.Id
                where m.UserId == userId || (p.UserId == userId && p.ApprovalStatus == ApprovalStatus.Approved)
                select new
                {
                    a.Id, a.Status, s.Date, s.StartHour, a.Topic, a.SessionEndedAt, a.SessionEndedBy,
                    MotherUserId = m.UserId, MotherName = m.DisplayName, ProfessionalName = p.DisplayName
                })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var now = PolandTime.Now;
        var me = row.MotherUserId == userId ? AppointmentParty.Mother : AppointmentParty.Professional;

        // A completed appointment (closing note written) is always read-only.
        var state = row.Status == AppointmentStatus.Completed
            ? RoomState.Closed
            : ConsultationRoom.StateOf(row.Date, row.StartHour, row.SessionEndedAt, now);
        var start = ConsultationRoom.Start(row.Date, row.StartHour);
        var opensAt = ConsultationRoom.OpensAt(row.Date, row.StartHour);
        var end = ConsultationRoom.End(row.Date, row.StartHour, row.SessionEndedAt);

        return new ChatRoomInfo(
            row.Id,
            me,
            me == AppointmentParty.Mother ? row.MotherName : row.ProfessionalName,
            me == AppointmentParty.Mother ? row.ProfessionalName : row.MotherName,
            row.MotherName,
            row.ProfessionalName,
            row.Date,
            row.StartHour,
            row.Topic,
            state,
            opensAt,
            end,
            row.SessionEndedBy,
            state == RoomState.Open && now >= start,
            SecondsUntil(opensAt, now),
            SecondsUntil(start, now),
            SecondsUntil(end, now));
    }

    private static ChatMessageItem ToItem(ChatMessage m, RoomState state, DateTime utcNow)
    {
        var editableFor = state == RoomState.Open ? m.SentAt + ChatMessage.EditWindow - utcNow : TimeSpan.Zero;
        return new ChatMessageItem(
            m.Id,
            m.Sender,
            m.Text,
            PolandTime.ToLocal(m.SentAt),
            m.EditedAt is not null,
            editableFor > TimeSpan.Zero ? (int)Math.Ceiling(editableFor.TotalSeconds) : 0);
    }

    private static int SecondsUntil(DateTime moment, DateTime now) =>
        moment > now ? (int)Math.Ceiling((moment - now).TotalSeconds) : 0;

    private static string? Normalize(string? text)
    {
        var normalized = text?.ReplaceLineEndings("\n").Trim();
        return string.IsNullOrEmpty(normalized) || normalized.Length > ChatMessage.TextMaxLength ? null : normalized;
    }
}
