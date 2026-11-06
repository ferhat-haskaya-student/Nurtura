using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// A past consultation as one of its two people sees it: the details, the closing note and the chat history.
// Room: the chat room (always closed here) with names, date, hour, topic, end and who ended it.
// Note: null until the professional has written it.
// CanWrite / CanEdit: the professional only — no note yet / note written less than 24 hours ago.
public sealed record Consultation(
    ChatRoomInfo Room,
    IReadOnlyList<ChatMessageItem> Messages,
    ProfessionalType ProfessionalType,
    string Concern,
    ClosingNote? Note,
    bool CanWrite,
    bool CanEdit);

// The closing note. Times are Poland time. EditableUntil: 24 hours after the first save.
public sealed record ClosingNote(string Text, DateTime WrittenAt, DateTime EditableUntil);

public enum NoteSaveResult
{
    NotAllowed,
    Created,
    Updated
}
