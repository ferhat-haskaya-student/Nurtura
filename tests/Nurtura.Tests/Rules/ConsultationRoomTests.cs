using Nurtura.Core;
using Nurtura.Core.Services;

namespace Nurtura.Tests.Rules;

// The chat room clock (pure rules, no database): opens 5 minutes before the start, closes at the end of the hour,
// or earlier when either side used End session. All times are Poland time.
public sealed class ConsultationRoomTests
{
    private static readonly DateOnly Day = new(2026, 11, 20);
    private const int Hour = 10;

    private static DateTime At(int hour, int minute, int second = 0) => Day.ToDateTime(new TimeOnly(hour, minute, second));

    [Theory]
    [InlineData(9, 54, 59, RoomState.NotYetOpen)]
    [InlineData(9, 55, 0, RoomState.Open)]
    [InlineData(10, 0, 0, RoomState.Open)]
    [InlineData(10, 59, 59, RoomState.Open)]
    [InlineData(11, 0, 0, RoomState.Closed)]
    public void StateOf_AtBoundary(int hour, int minute, int second, RoomState expected)
    {
        Assert.Equal(expected, ConsultationRoom.StateOf(Day, Hour, null, At(hour, minute, second)));
    }

    [Fact]
    public void StateOf_EndedEarly_ClosedFromEndTime()
    {
        Assert.True(PolandTime.TryToUtc(Day, new TimeOnly(10, 20), out var endedAtUtc));

        Assert.Equal(RoomState.Open, ConsultationRoom.StateOf(Day, Hour, endedAtUtc, At(10, 19, 59)));
        Assert.Equal(RoomState.Closed, ConsultationRoom.StateOf(Day, Hour, endedAtUtc, At(10, 20)));
        Assert.Equal(RoomState.Closed, ConsultationRoom.StateOf(Day, Hour, endedAtUtc, At(10, 45)));
    }
}
