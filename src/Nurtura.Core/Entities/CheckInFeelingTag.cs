namespace Nurtura.Core.Entities;

// One row per feeling tag ticked on a check-in.
public class CheckInFeelingTag
{
    public int DailyCheckInId { get; set; }

    public int FeelingTagId { get; set; }
}
