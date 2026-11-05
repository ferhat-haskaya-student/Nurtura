using Nurtura.Core.Entities;

namespace Nurtura.Web.Hubs;

// Who has a consultation chat open right now, kept in memory: appointment → side → open connections.
// One person can have the chat open on two devices; she is "here" while at least one is connected.
public class ChatPresence
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, Dictionary<AppointmentParty, HashSet<string>>> _rooms = [];
    private readonly Dictionary<string, (int AppointmentId, AppointmentParty Party)> _connections = [];

    // Records the connection in the room. Called only after ChatService has confirmed who the user is.
    public void Join(string connectionId, int appointmentId, AppointmentParty party)
    {
        lock (_lock)
        {
            RemoveLocked(connectionId);
            if (!_rooms.TryGetValue(appointmentId, out var sides))
            {
                sides = [];
                _rooms[appointmentId] = sides;
            }

            if (!sides.TryGetValue(party, out var connections))
            {
                connections = [];
                sides[party] = connections;
            }

            connections.Add(connectionId);
            _connections[connectionId] = (appointmentId, party);
        }
    }

    // Forgets the connection. Returns the room and side it was in, and whether that side is still here
    // on another connection; null when the connection had not joined a room.
    public (int AppointmentId, AppointmentParty Party, bool StillHere)? Leave(string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(connectionId, out var entry))
            {
                return null;
            }

            RemoveLocked(connectionId);
            return (entry.AppointmentId, entry.Party, IsHereLocked(entry.AppointmentId, entry.Party));
        }
    }

    public bool IsHere(int appointmentId, AppointmentParty party)
    {
        lock (_lock)
        {
            return IsHereLocked(appointmentId, party);
        }
    }

    private bool IsHereLocked(int appointmentId, AppointmentParty party) =>
        _rooms.TryGetValue(appointmentId, out var sides) && sides.TryGetValue(party, out var connections) && connections.Count > 0;

    private void RemoveLocked(string connectionId)
    {
        if (!_connections.Remove(connectionId, out var entry))
        {
            return;
        }

        var sides = _rooms[entry.AppointmentId];
        sides[entry.Party].Remove(connectionId);
        if (sides[entry.Party].Count == 0)
        {
            sides.Remove(entry.Party);
        }

        if (sides.Count == 0)
        {
            _rooms.Remove(entry.AppointmentId);
        }
    }
}
