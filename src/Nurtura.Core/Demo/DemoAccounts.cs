using Nurtura.Core.Entities;

namespace Nurtura.Core.Demo;

// The demo accounts (design doc §4 "Demo data"). The domain cannot belong to anyone, like the tests' @test.nurtura.
public static class DemoAccounts
{
    public const string KasiaEmail = "kasia@demo.nurtura";
    public const string MartaEmail = "marta@demo.nurtura";
    public const string OlaEmail = "ola@demo.nurtura";

    public const string AnnaEmail = "anna@demo.nurtura";
    public const string EwaEmail = "ewa@demo.nurtura";
    public const string MagdaEmail = "magda@demo.nurtura";
    public const string JoannaEmail = "joanna@demo.nurtura";
    public const string NataliaEmail = "natalia@demo.nurtura";

    public static readonly string[] MotherEmails = [KasiaEmail, MartaEmail, OlaEmail];

    public static readonly string[] ProfessionalEmails = [AnnaEmail, EwaEmail, MagdaEmail, JoannaEmail, NataliaEmail];

    public static IEnumerable<string> All => MotherEmails.Concat(ProfessionalEmails);

    internal sealed record ProfessionalAccount(
        string Email,
        string DisplayName,
        ProfessionalType Type,
        ApprovalStatus Status,
        Topic[] Topics,
        Language[] Languages,
        string Bio,
        // Her open hours each week (Poland time, start hours); none for the pending one.
        IReadOnlyDictionary<DayOfWeek, int[]> Hours);

    private static readonly int[] AnnaHours = [9, 10, 11, 12];
    private static readonly int[] EwaHours = [10, 11, 12, 13, 14, 15];
    private static readonly int[] EwaSaturday = [9, 10, 11];
    private static readonly int[] MagdaHours = [14, 15, 16, 17];
    private static readonly int[] JoannaHours = [8, 9, 10, 11];

    internal static readonly ProfessionalAccount Anna = new(
        AnnaEmail,
        "Anna Nowak",
        ProfessionalType.Midwife,
        ApprovalStatus.Approved,
        [Topic.RecoveryAfterBirth, Topic.PostpartumWellbeing, Topic.BabyFeeding],
        [Language.Polish, Language.English],
        "Community midwife for 12 years. I help with recovery after birth, the first weeks at home and feeding " +
        "questions. Happy to talk in Polish or English.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Monday] = AnnaHours,
            [DayOfWeek.Wednesday] = AnnaHours,
            [DayOfWeek.Friday] = AnnaHours
        });

    internal static readonly ProfessionalAccount Ewa = new(
        EwaEmail,
        "Ewa Kowalska",
        ProfessionalType.LactationConsultant,
        ApprovalStatus.Approved,
        [Topic.BabyFeeding, Topic.Nutrition],
        [Language.Polish, Language.English],
        "Certified lactation consultant and mum of two. Breastfeeding, pumping, bottles, combining both, and " +
        "starting solids.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Tuesday] = EwaHours,
            [DayOfWeek.Thursday] = EwaHours,
            [DayOfWeek.Saturday] = EwaSaturday
        });

    internal static readonly ProfessionalAccount Magda = new(
        MagdaEmail,
        "Magda Wiśniewska",
        ProfessionalType.SleepConsultant,
        ApprovalStatus.Approved,
        [Topic.BabySleep],
        [Language.Polish, Language.English],
        "Baby sleep consultant. I help families find a daily rhythm that works for them, gently and at the " +
        "baby's own pace.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Monday] = MagdaHours,
            [DayOfWeek.Tuesday] = MagdaHours,
            [DayOfWeek.Wednesday] = MagdaHours,
            [DayOfWeek.Thursday] = MagdaHours,
            [DayOfWeek.Friday] = MagdaHours
        });

    internal static readonly ProfessionalAccount Joanna = new(
        JoannaEmail,
        "Joanna Zielińska",
        ProfessionalType.Psychologist,
        ApprovalStatus.Approved,
        [Topic.EmotionalSupport, Topic.PostpartumWellbeing],
        [Language.Polish, Language.English],
        "Psychologist working with new parents. A calm hour to talk about how you feel, the changes at home and " +
        "life with a baby.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Monday] = JoannaHours,
            [DayOfWeek.Tuesday] = JoannaHours,
            [DayOfWeek.Thursday] = JoannaHours
        });

    internal static readonly ProfessionalAccount Natalia = new(
        NataliaEmail,
        "Natalia Lewandowska",
        ProfessionalType.Dietitian,
        ApprovalStatus.Pending,
        [Topic.Nutrition],
        [Language.Polish],
        "Dietitian. Eating well after birth and while breastfeeding, with simple meals for busy days.",
        new Dictionary<DayOfWeek, int[]>());

    internal static readonly ProfessionalAccount[] Professionals = [Anna, Ewa, Magda, Joanna, Natalia];
}
