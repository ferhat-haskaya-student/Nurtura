using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Content;

// The approved guidance pages, seeded straight into the database until the admin panel takes over editing.
// Safe to run on every start: a category is added only if no category has its name, a page only if no
// page has its slug. Existing rows are never changed, so later edits in the admin panel are kept.
// New approved pages are added to Pages.
public static class GuidanceSeeder
{
    private const string EmotionalWellbeing = "Emotional wellbeing";
    private const string Sleep = "Sleep";
    private const string NutritionByAge = "Nutrition by age";

    private static readonly DateOnly Checked = new(2026, 11, 6);
    private static readonly DateOnly NextCheck = new(2027, 11, 6);

    private const string CuhSignsName = "Cambridge University Hospitals NHS: Signs and symptoms and who to call";
    private const string CuhSignsUrl = "https://www.cuh.nhs.uk/rosie-hospital/maternity/after-birth/signs-and-symptoms-and-who-to-call/";

    public static async Task SeedAsync(INurturaDbContext db, CancellationToken cancellationToken = default)
    {
        var categories = await db.GuidanceCategories.ToDictionaryAsync(c => c.Name, cancellationToken);
        foreach (var name in Pages.Select(p => p.Category).Distinct())
        {
            if (!categories.ContainsKey(name))
            {
                var category = new GuidanceCategory { Name = name };
                db.GuidanceCategories.Add(category);
                categories[name] = category;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var existingSlugs = await db.GuidanceContents.Select(g => g.Slug).ToListAsync(cancellationToken);
        foreach (var page in Pages.Where(p => !existingSlugs.Contains(p.Slug)))
        {
            db.GuidanceContents.Add(new GuidanceContent
            {
                Slug = page.Slug,
                GuidanceCategoryId = categories[page.Category].Id,
                Audience = page.Audience,
                Title = page.Title,
                Summary = page.Summary,
                Body = page.Body,
                RelatedTopic = page.Topic,
                BabyAgeFromMonths = page.AgeFrom,
                BabyAgeToMonths = page.AgeTo,
                GetHelpHeading = page.GetHelpHeading,
                GetHelpItems = page.GetHelpItems.ToList(),
                EmergencyHeading = page.EmergencyHeading,
                EmergencyItems = page.EmergencyItems.ToList(),
                Language = Language.English,
                LastChecked = Checked,
                NextCheck = NextCheck,
                IsPublished = true,
                Sources = page.Sources
                    .Select((s, i) => new GuidanceSource { Name = s.Name, Url = s.Url, SortOrder = i + 1 })
                    .ToList()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedSource(string Name, string Url);

    private sealed record SeedPage(
        string Slug,
        GuidanceAudience Audience,
        string Category,
        Topic Topic,
        int? AgeFrom,
        int? AgeTo,
        string Title,
        string Summary,
        string Body,
        string? EmergencyHeading,
        string[] EmergencyItems,
        string GetHelpHeading,
        string[] GetHelpItems,
        SeedSource[] Sources);

    // Copied exactly from the approved content (docs/nurtura_guidance_content.md, approved by F on 06-11-2026).
    private static readonly SeedPage[] Pages =
    [
        new SeedPage(
            Slug: "when-to-seek-professional-support",
            Audience: GuidanceAudience.Mother,
            Category: EmotionalWellbeing,
            Topic: Topic.PostpartumWellbeing,
            AgeFrom: null,
            AgeTo: null,
            Title: "When to seek professional support",
            Summary: "How to tell when a worry needs a professional, and where to turn in Poland.",
            Body: """
                ## It's always OK to ask

                Your body and your feelings change a lot after birth, and much of it is normal. But you never need a "good enough" reason to contact a midwife or doctor. If you feel that something is not right, that is reason enough.

                ## Your body after birth

                Contact a professional when something gets worse instead of better, for example bleeding that gets heavier, a fever, or a wound that looks red or opens. The cards below list the signs to watch for.

                ## Your feelings

                Feeling down, anxious or irritable for a few days after birth is very common. It's often called the "baby blues", and it usually passes within 2 weeks. If it lasts longer, gets worse, or you are struggling to cope, it may be postnatal depression. It can start in pregnancy or any time in the first year after birth. Signs include low mood, finding it hard to enjoy anything, feeling hopeless or guilty, worrying all the time, and trouble sleeping even when you get the chance to rest. Getting help matters even if you only have some of these signs. Postnatal depression can be treated.

                ## Who you can contact in Poland

                - Your community midwife (położna POZ) or family doctor, for most worries.
                - Night and holiday care (nocna i świąteczna opieka zdrowotna), when your clinic is closed. It is open weekday evenings until morning, and all day on weekends and public holidays.
                - The First Contact Teleplatform: 800 137 200 (free phone advice from a nurse, midwife or doctor, also in English). It is open at the same hours as night and holiday care.
                - The NFZ patient line: 800 190 590 (free, 24 hours). They can tell you the nearest hospital or night clinic.
                - If you are in emotional crisis: 800 70 2222 (free, 24 hours) or 116 123 (24 hours).

                ## Nurtura consultations

                A Nurtura professional can talk with you about recovery, feelings, sleep or feeding. But a consultation is booked in advance and is not for urgent problems. For those, use the contacts above.
                """,
            EmergencyHeading: "Call 112 now if:",
            EmergencyItems:
            [
                "your bleeding suddenly gets much heavier, or you feel faint, dizzy or have a pounding heart",
                "you have chest pain or find it hard to breathe",
                "you have thoughts of harming yourself or your baby and feel you might act on them"
            ],
            GetHelpHeading: "Contact your midwife or doctor straight away if:",
            GetHelpItems:
            [
                "you have a fever, shivering, or tummy pain, especially with bad-smelling discharge",
                "you have a headache, blurred vision or vomiting",
                "you have pain, redness or swelling in your calf",
                "your wound or stitches are red, leaking, or seem to be opening",
                "it hurts to pee",
                "you still can't control your pee or poo more than 6 weeks after the birth",
                "low mood lasts more than 2 weeks, or you notice signs of postnatal depression",
                "you have thoughts of harming yourself or your baby, even if you would not act on them",
                "you simply feel that something is not right"
            ],
            Sources:
            [
                new("NHS: Postnatal depression symptoms",
                    "https://www.nhs.uk/mental-health/conditions/post-natal-depression/symptoms/"),
                new("HSE: Medical help after birth",
                    "https://www2.hse.ie/pregnancy-birth/birth/health-after-birth/medical-help-after-birth/"),
                new(CuhSignsName, CuhSignsUrl),
                new("Ministry of Health (gov.pl): Gdzie uzyskać pomoc psychologiczną i psychiatryczną",
                    "https://www.gov.pl/web/zdrowie/gdzie-uzyskac-pomoc-psychologiczna-i-psychiatryczna"),
                new("NFZ: Gdzie się leczyć w majówkę",
                    "https://www.nfz.gov.pl/aktualnosci/aktualnosci-centrali/nfz-przypomina-gdzie-sie-leczyc-w-majowke,8384.html")
            ]),

        new SeedPage(
            Slug: "baby-sleep-routine",
            Audience: GuidanceAudience.Baby,
            Category: Sleep,
            Topic: Topic.BabySleep,
            AgeFrom: null,
            AgeTo: null,
            Title: "Baby sleep routine",
            Summary: "How babies sleep in the first year, a simple bedtime routine, and how to keep sleep safe.",
            Body: """
                ## How babies sleep

                Newborns sleep more than they are awake, often up to about 18 hours a day, but in short stretches. Waking at night is normal, because young babies need to feed. As they grow, most babies need fewer night feeds. Between 3 and 6 months, some babies sleep 5 to 8 hours or more at night, but not all. Every baby is different, and sleep often changes again with growth spurts, teething or illness.

                ## Day and night

                You can help your baby learn that night is for sleeping. In the day, open the curtains, play, and don't worry about normal household noise during naps. At night, keep the lights low and your voice quiet, and put your baby down soon after a feed and a nappy change.

                ## A simple bedtime routine

                A simple, calm bedtime routine can help your baby settle. You don't need all of these. Pick what works for you:

                - a bath
                - night clothes and a fresh nappy
                - dim lights
                - a quiet story or a lullaby
                - a cuddle and putting your baby down

                ## Safe sleep

                - Always put your baby on their back to sleep, with their feet at the bottom of the cot (feet to foot).
                - The safest place is their own cot or Moses basket, in the same room as you, for at least the first 6 months.
                - Use a firm, flat mattress. Keep the cot clear of pillows, duvets, bumpers, loose blankets and soft toys.
                - Don't let your baby get too hot. If their skin is hot or sweaty, remove a layer.
                - Don't smoke around your baby.
                - Never fall asleep with your baby on a sofa or in an armchair.
                - Don't share a bed with your baby if you or your partner smoke, have drunk alcohol, have taken drugs, or are extremely tired, or if your baby is unwell.

                ## Looking after yourself too

                Broken nights are hard. If you can, share night duties with a partner, or ask a friend or relative for help. If sleep problems go on, or you would like help with a routine, talk to your community midwife or your baby's doctor.
                """,
            EmergencyHeading: "Call 112 now if your baby:",
            EmergencyItems:
            [
                "is very sleepy, limp or floppy, or does not respond to you",
                "has fast or noisy breathing, or blue lips",
                "has a fit"
            ],
            GetHelpHeading: "Contact your midwife or doctor the same day if:",
            GetHelpItems:
            [
                "your baby is very sleepy and feeding poorly",
                "your baby cries a lot and is hard to settle",
                "you are worried about your baby"
            ],
            Sources:
            [
                new("NHS: Helping your baby to sleep",
                    "https://www.nhs.uk/baby/caring-for-a-newborn/helping-your-baby-to-sleep/"),
                new("NHS Start for Life: Safe sleep advice for babies",
                    "https://www.nhs.uk/start-for-life/baby/baby-basics/newborn-and-baby-sleeping-advice-for-parents/safe-sleep-advice-for-babies"),
                new("NHS: Reducing the risk of sudden infant death syndrome",
                    "https://www.nhs.uk/conditions/pregnancy-and-baby/reducing-risk-cot-death"),
                new(CuhSignsName, CuhSignsUrl)
            ]),

        new SeedPage(
            Slug: "baby-nutrition-3-to-6-months",
            Audience: GuidanceAudience.Baby,
            Category: NutritionByAge,
            Topic: Topic.BabyFeeding,
            AgeFrom: 3,
            AgeTo: 6,
            Title: "Baby nutrition: 3 to 6 months",
            Summary: "Milk is all your baby needs for now. How to tell when your baby is ready for first foods.",
            Body: """
                ## Milk is enough for now

                The WHO recommends breast milk only for the first 6 months, with no other food or drink, not even water. Feed your baby when they show they are hungry, day and night.

                ## When to start solid food

                The NHS advises waiting until your baby is around 6 months old. This applies whether your baby has breast milk, formula or both. Don't start early, even if a food label says "from 4 months". If your baby was born early, ask your midwife or doctor when to start.

                ## Signs your baby is ready

                These three signs usually appear together, from around 6 months. Your baby can:

                - sit up and hold their head steady
                - look at food, pick it up and put it in their mouth
                - swallow food, rather than push it back out

                ## Signs that are often mistaken for readiness

                These are normal baby behaviors, and they don't mean your baby needs solid food:

                - chewing their fists
                - wanting extra milk feeds
                - waking more at night than before

                Starting solids won't make your baby sleep through the night. If your baby seems hungrier, extra milk feeds can help until they are ready.

                ## Getting ready

                Packaged baby biscuits, snacks and sweet fruit pouches can be high in sugar. Babies under 12 months don't need snacks. If your baby seems hungry between meals, offer extra milk instead.
                """,
            EmergencyHeading: "Call 112 now if your baby:",
            EmergencyItems:
            [
                "is very sleepy, limp or floppy, or does not respond to you",
                "has fast or noisy breathing, or blue lips"
            ],
            GetHelpHeading: "Contact your midwife or doctor straight away if:",
            GetHelpItems:
            [
                "your baby is feeding much less than usual",
                "your baby has far fewer wet nappies than usual",
                "you are worried about your baby"
            ],
            Sources:
            [
                new("WHO: Breastfeeding",
                    "https://www.who.int/westernpacific/health-topics/breastfeeding/1"),
                new("NHS Start for Life: How to start weaning your baby",
                    "https://www.nhs.uk/start-for-life/baby/weaning/how-to-start-weaning-your-baby/"),
                new(CuhSignsName, CuhSignsUrl)
            ])
    ];
}
