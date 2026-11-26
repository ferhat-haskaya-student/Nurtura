using Nurtura.Core.Entities;

namespace Nurtura.Core.Demo;

// The demo accounts (design doc §4 "Demo data"). The domain cannot belong to anyone, like the tests' @test.nurtura.
public static class DemoAccounts
{
    // Named by type, so nobody has to remember which name is which (F, 26 Nov 2026).
    public const string Domain = "@demo.nurtura";

    public const string Mother1Email = "mother1" + Domain;
    public const string Mother2Email = "mother2" + Domain;
    public const string Mother3Email = "mother3" + Domain;

    public const string MidwifeEmail = "midwife" + Domain;
    public const string LactationEmail = "lactation" + Domain;
    public const string SleepEmail = "sleep" + Domain;
    public const string PsychologistEmail = "psychologist" + Domain;
    public const string DietitianEmail = "dietitian" + Domain;

    public static readonly string[] MotherEmails = [Mother1Email, Mother2Email, Mother3Email];

    public static readonly string[] ProfessionalEmails =
        [MidwifeEmail, LactationEmail, SleepEmail, PsychologistEmail, DietitianEmail];

    public static IEnumerable<string> All => MotherEmails.Concat(ProfessionalEmails);

    // Every demo account, also those of an earlier run with other names.
    public static bool IsDemo(string? email) =>
        email is not null && email.EndsWith(Domain, StringComparison.OrdinalIgnoreCase);

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

    private static readonly int[] MidwifeHours = [9, 10, 11, 12];
    private static readonly int[] LactationHours = [10, 11, 12, 13, 14, 15];
    private static readonly int[] LactationSaturday = [9, 10, 11];
    private static readonly int[] SleepHours = [14, 15, 16, 17];
    private static readonly int[] PsychologistHours = [8, 9, 10, 11];

    internal static readonly ProfessionalAccount Midwife = new(
        MidwifeEmail,
        "Midwife",
        ProfessionalType.Midwife,
        ApprovalStatus.Approved,
        [Topic.RecoveryAfterBirth, Topic.PostpartumWellbeing, Topic.BabyFeeding],
        [Language.Polish, Language.English],
        "Community midwife for 12 years. I help with recovery after birth, the first weeks at home and feeding " +
        "questions. Happy to talk in Polish or English.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Monday] = MidwifeHours,
            [DayOfWeek.Wednesday] = MidwifeHours,
            [DayOfWeek.Friday] = MidwifeHours
        });

    internal static readonly ProfessionalAccount Lactation = new(
        LactationEmail,
        "Lactation consultant",
        ProfessionalType.LactationConsultant,
        ApprovalStatus.Approved,
        [Topic.BabyFeeding, Topic.Nutrition],
        [Language.Polish, Language.English],
        "Certified lactation consultant and mum of two. Breastfeeding, pumping, bottles, combining both, and " +
        "starting solids.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Tuesday] = LactationHours,
            [DayOfWeek.Thursday] = LactationHours,
            [DayOfWeek.Saturday] = LactationSaturday
        });

    internal static readonly ProfessionalAccount Sleep = new(
        SleepEmail,
        "Sleep consultant",
        ProfessionalType.SleepConsultant,
        ApprovalStatus.Approved,
        [Topic.BabySleep],
        [Language.Polish, Language.English],
        "Baby sleep consultant. I help families find a daily rhythm that works for them, gently and at the " +
        "baby's own pace.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Monday] = SleepHours,
            [DayOfWeek.Tuesday] = SleepHours,
            [DayOfWeek.Wednesday] = SleepHours,
            [DayOfWeek.Thursday] = SleepHours,
            [DayOfWeek.Friday] = SleepHours
        });

    internal static readonly ProfessionalAccount Psychologist = new(
        PsychologistEmail,
        "Psychologist",
        ProfessionalType.Psychologist,
        ApprovalStatus.Approved,
        [Topic.EmotionalSupport, Topic.PostpartumWellbeing],
        [Language.Polish, Language.English],
        "Psychologist working with new parents. A calm hour to talk about how you feel, the changes at home and " +
        "life with a baby.",
        new Dictionary<DayOfWeek, int[]>
        {
            [DayOfWeek.Monday] = PsychologistHours,
            [DayOfWeek.Tuesday] = PsychologistHours,
            [DayOfWeek.Thursday] = PsychologistHours
        });

    internal static readonly ProfessionalAccount Dietitian = new(
        DietitianEmail,
        "Dietitian",
        ProfessionalType.Dietitian,
        ApprovalStatus.Pending,
        [Topic.Nutrition],
        [Language.Polish],
        "Dietitian. Eating well after birth and while breastfeeding, with simple meals for busy days.",
        new Dictionary<DayOfWeek, int[]>());

    internal static readonly ProfessionalAccount[] Professionals = [Midwife, Lactation, Sleep, Psychologist, Dietitian];
}
