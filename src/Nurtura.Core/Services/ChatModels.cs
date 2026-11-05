using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// One consultation chat room as the logged-in user sees it. Times are Poland time.
// Me: her side of the appointment. MyName / OtherName: the two display names from her side.
// End: the real end (end of the hour, or when End session was used). EndedBy: who used End session, if anyone.
// CanEndSession: the room is open and the session has started.
// SecondsToOpen / SecondsToStart / SecondsToClose: from the server clock, 0 once passed. The page sets its
// timers from these, so it never depends on the phone's clock.
public sealed record ChatRoomInfo(
    int AppointmentId,
    AppointmentParty Me,
    string MyName,
    string OtherName,
    string MotherName,
    string ProfessionalName,
    DateOnly Date,
    int Hour,
    Topic Topic,
    RoomState State,
    DateTime OpensAt,
    DateTime End,
    AppointmentParty? EndedBy,
    bool CanEndSession,
    int SecondsToOpen,
    int SecondsToStart,
    int SecondsToClose)
{
    public string NameOf(AppointmentParty party) => party == AppointmentParty.Mother ? MotherName : ProfessionalName;
}

// One message. SentAt is Poland time. EditableForSeconds: how long its sender can still edit it (0 when the
// window has passed or the room is not open); the page shows Edit only on her own messages.
public sealed record ChatMessageItem(int Id, AppointmentParty Sender, string Text, DateTime SentAt, bool Edited, int EditableForSeconds);

public sealed record ChatRoom(ChatRoomInfo Info, IReadOnlyList<ChatMessageItem> Messages);

// A message that was sent or edited, with the room it belongs to.
public sealed record ChatSendResult(ChatRoomInfo Room, ChatMessageItem Message);
