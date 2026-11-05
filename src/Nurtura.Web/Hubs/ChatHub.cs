using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Nurtura.Core.Entities;
using Nurtura.Core.Identity;
using Nurtura.Core.Services;
using Nurtura.Web.Localization;

namespace Nurtura.Web.Hubs;

// The consultation chat over SignalR. The user is always Context.UserIdentifier (the logged-in Identity user);
// the page never says who she is. Every method checks again through ChatService that she is the mother or the
// approved professional of that appointment and, for changes, that the room is open by the server clock.
// Nothing is trusted from an earlier call. The admin role cannot connect at all.
[Authorize(Roles = Roles.Mother + "," + Roles.Professional)]
public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly ChatPresence _presence;

    public ChatHub(ChatService chatService, ChatPresence presence)
    {
        _chatService = chatService;
        _presence = presence;
    }

    // Opens the room on this connection: the room state, all messages, and whether the other side is here.
    // Allowed before the room opens and after it closes (read-only); the page calls it again after a reconnect
    // and when its open / start / close timer fires, so nothing is missed.
    public async Task<object> JoinRoom(int appointmentId)
    {
        var room = await _chatService.GetRoomAsync(UserId, appointmentId) ?? throw new HubException("NotFound");
        var info = room.Info;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(appointmentId));
        var wasHere = _presence.IsHere(appointmentId, info.Me);
        _presence.Join(Context.ConnectionId, appointmentId, info.Me);
        if (!wasHere)
        {
            await Clients.OthersInGroup(GroupName(appointmentId)).SendAsync("PresenceChanged", info.Me.ToString(), true);
        }

        var other = info.Me == AppointmentParty.Mother ? AppointmentParty.Professional : AppointmentParty.Mother;
        return new
        {
            state = info.State.ToString(),
            me = info.Me.ToString(),
            otherHere = _presence.IsHere(appointmentId, other),
            canEndSession = info.CanEndSession,
            secondsToOpen = info.SecondsToOpen,
            secondsToStart = info.SecondsToStart,
            secondsToClose = info.SecondsToClose,
            end = Time(info.End),
            endedByName = info.EndedBy is { } endedBy ? info.NameOf(endedBy) : null,
            messages = room.Messages.Select(ToClient).ToList()
        };
    }

    public async Task SendMessage(int appointmentId, string text)
    {
        var result = await _chatService.SendAsync(UserId, appointmentId, text) ?? throw new HubException("CantSend");
        await Clients.Group(GroupName(appointmentId)).SendAsync("MessageReceived", ToClient(result.Message));
    }

    public async Task EditMessage(int appointmentId, int messageId, string text)
    {
        var result = await _chatService.EditAsync(UserId, appointmentId, messageId, text) ?? throw new HubException("CantEdit");
        await Clients.Group(GroupName(appointmentId)).SendAsync("MessageEdited", ToClient(result.Message));
    }

    public async Task EndSession(int appointmentId)
    {
        var room = await _chatService.EndSessionAsync(UserId, appointmentId) ?? throw new HubException("CantEnd");
        await Clients.Group(GroupName(appointmentId)).SendAsync("SessionEnded", room.MyName);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_presence.Leave(Context.ConnectionId) is { StillHere: false } left)
        {
            await Clients.Group(GroupName(left.AppointmentId)).SendAsync("PresenceChanged", left.Party.ToString(), false);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string UserId => Context.UserIdentifier ?? throw new HubException("NotFound");

    private static string GroupName(int appointmentId) => $"appointment-{appointmentId}";

    private static string Time(DateTime polandTime) => polandTime.ToString(TimeFormats.Display, CultureInfo.InvariantCulture);

    // Plain data only: the page puts every string on screen with textContent.
    private static object ToClient(ChatMessageItem m) => new
    {
        id = m.Id,
        sender = m.Sender.ToString(),
        text = m.Text,
        time = Time(m.SentAt),
        edited = m.Edited,
        editableFor = m.EditableForSeconds
    };
}
