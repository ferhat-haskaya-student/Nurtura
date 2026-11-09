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
    private const string Recovery = "Recovery";
    private const string SleepAndTiredness = "Sleep and tiredness";
    private const string SelfCare = "Self-care";
    private const string Nutrition = "Nutrition";
    private const string Feeding = "Feeding";
    private const string Crying = "Crying";
    private const string DailyLife = "Daily life";

    private static readonly DateOnly Checked = new(2026, 11, 6);
    private static readonly DateOnly NextCheck = new(2027, 11, 6);

    // Pages 4 to 17 were checked later than the first three.
    private static readonly DateOnly Checked09Nov = new(2026, 11, 9);
    private static readonly DateOnly NextCheck09Nov = new(2027, 11, 9);

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
                LastChecked = page.LastChecked ?? Checked,
                NextCheck = page.NextCheckOn ?? NextCheck,
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
        SeedSource[] Sources,
        DateOnly? LastChecked = null,
        DateOnly? NextCheckOn = null);

    // Copied exactly from the approved content (docs/nurtura_guidance_content.md): pages 1 to 3 approved by F on
    // 06-11-2026, pages 4 to 17 on 09-11-2026.
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
            ]),

        new SeedPage(
            Slug: "recovery-after-birth",
            Audience: GuidanceAudience.Mother,
            Category: Recovery,
            Topic: Topic.RecoveryAfterBirth,
            AgeFrom: null,
            AgeTo: null,
            Title: "Recovery after birth",
            Summary: "What to expect from your body in the first weeks after birth, including after a caesarean.",
            Body: """
                ## Bleeding

                After birth you will bleed from the vagina. It is heavy at first, then slowly turns brownish and gets less until it stops. This carries on for a few weeks. It can look redder and heavier when you breastfeed. Use pads, not tampons, until your postnatal check, and tell your midwife if you pass large clots.

                ## Stitches and soreness

                If you have stitches, wash them every day with plain warm water in a bath or shower, then pat yourself dry. Most stitches dissolve on their own as you heal. Drinking plenty of water can make peeing sting less.

                ## Bowels

                It is normal not to poo for a few days after birth. Eat foods with fibre, drink water, and try not to strain. Piles usually go away within a few days or weeks. A midwife or pharmacist can suggest a soothing cream. If constipation does not go away, talk to your doctor.

                ## Bladder and pelvic floor

                Leaking a little pee when you laugh, cough or move suddenly is common after birth. Pelvic floor exercises can help. If they don't, mention it at your postnatal check.

                ## Breasts

                On the third or fourth day, your breasts may feel tight and tender as your milk comes in. A supportive nursing bra can help.

                ## After a caesarean

                - Most women feel sore for the first few days. For some, it lasts several weeks. Have painkillers at home, and ask your midwife or pharmacist which ones are right for you, especially if you are breastfeeding.
                - Clean and dry your wound gently every day, and wear loose clothes and cotton underwear.
                - Keep gently active, for example a short walk every day. This lowers the risk of blood clots.
                - You can hold and carry your baby. But for about 6 weeks, avoid lifting anything heavier than your baby, exercise and sex, until they feel comfortable. You won't be able to drive for a few weeks.
                """,
            EmergencyHeading: "Call 112 now if:",
            EmergencyItems:
            [
                "your bleeding suddenly gets much heavier, or you feel faint, dizzy or have a pounding heart",
                "you have chest pain or find it hard to breathe"
            ],
            GetHelpHeading: "Contact your midwife or doctor straight away if:",
            GetHelpItems:
            [
                "you have severe pain",
                "you have heavy bleeding or pass large clots",
                "you have a fever or shivering",
                "your wound gets more red, painful or swollen, or leaks pus or bad-smelling fluid",
                "peeing is very painful or very difficult",
                "you have a cough or feel short of breath",
                "you have swelling or pain in your lower leg"
            ],
            Sources:
            [
                new("NHS: Your body after the birth",
                    "https://www.nhs.uk/pregnancy/labour-and-birth/after-the-birth/your-body/"),
                new("NHS: Caesarean section recovery",
                    "https://www.nhs.uk/conditions/caesarean-section/recovery/"),
                new("HSE: Medical help after birth",
                    "https://www2.hse.ie/pregnancy-birth/birth/health-after-birth/medical-help-after-birth/"),
                new(CuhSignsName, CuhSignsUrl)
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "your-feelings-after-birth",
            Audience: GuidanceAudience.Mother,
            Category: EmotionalWellbeing,
            Topic: Topic.EmotionalSupport,
            AgeFrom: null,
            AgeTo: null,
            Title: "Your feelings after birth",
            Summary: "Why the first weeks can feel like an emotional rollercoaster, and what helps.",
            Body: """
                ## Many feelings at once

                The first weeks and months with a baby can bring joy and pride, but also worry, sadness and frustration. Many parents find it hard to talk about the difficult feelings because they feel they should be happy. You are not doing anything wrong by feeling this way.

                ## The baby blues

                Feeling tearful, irritable, low or anxious in the first week after birth is very common. This is often called the baby blues. It is linked to the changes in your body after birth, recovering from the birth, and caring for a newborn on little sleep. It usually lasts a few days.

                These things can help:

                - rest when your baby sleeps
                - eat well and drink plenty of water
                - take a short walk
                - take short breaks while someone else holds the baby

                If low mood lasts longer than 2 weeks or gets worse, talk to your midwife or doctor. It may be postnatal depression, which can be treated.

                ## If the birth was hard

                Some women feel their birth was frightening or left them feeling helpless or out of control. Afterwards you might have unwanted memories that feel very real, feel on edge or numb, sleep badly, or keep going over what happened. These are normal reactions to a very stressful experience. They don't mean you are a bad parent.

                It can help to stay close to people you trust, accept practical help so you can rest, and keep up the basics like regular meals and gentle exercise. Many hospitals let you talk through your birth with a midwife afterwards. You can ask your maternity unit if this is offered.

                ## You are not alone

                Up to 1 in 5 women have mental health problems such as depression or anxiety during pregnancy or in the first year after birth. Talking to your midwife or doctor is the first step to getting support.
                """,
            EmergencyHeading: "Call 112 now if:",
            EmergencyItems:
            [
                "you have thoughts of harming yourself or your baby and feel you might act on them"
            ],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "low mood lasts more than 2 weeks or keeps getting worse",
                "you can't enjoy anything or feel hopeless",
                "memories of the birth still upset you a month or more later",
                "you have thoughts of harming yourself or your baby, even if you would not act on them",
                "you are worried about how you feel"
            ],
            Sources:
            [
                new("NHS Start for Life: Your mental health",
                    "https://www.nhs.uk/start-for-life/baby/your-mental-health/"),
                new("Tommy's: Feeling low after childbirth (baby blues)",
                    "https://tommys.org/pregnancy-information/im-pregnant/ask-a-midwife/feeling-low-after-childbirth-what-are-baby-blues"),
                new("Guy's and St Thomas' NHS: Looking after yourself",
                    "https://www.guysandstthomas.nhs.uk/our-services/maternity-after-your-baby-born/looking-after-yourself"),
                new("Kingston and Richmond NHS: Looking after your mental health after a traumatic birth",
                    "https://www.kingstonandrichmond.nhs.uk/patients-and-families/patient-leaflets/looking-after-your-mental-health-after-traumatic-birth"),
                new("NHS: Postnatal depression symptoms",
                    "https://www.nhs.uk/mental-health/conditions/post-natal-depression/symptoms/"),
                new("Ministry of Health (gov.pl): Gdzie uzyskać pomoc psychologiczną i psychiatryczną",
                    "https://www.gov.pl/web/zdrowie/gdzie-uzyskac-pomoc-psychologiczna-i-psychiatryczna")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "coping-with-stress",
            Audience: GuidanceAudience.Mother,
            Category: EmotionalWellbeing,
            Topic: Topic.EmotionalSupport,
            AgeFrom: null,
            AgeTo: null,
            Title: "Coping with stress",
            Summary: "How stress can show up when you have a new baby, and small things that can ease it.",
            Body: """
                ## How stress can feel

                Life with a new baby can feel like too much at times. Stress can show in your feelings, your body and what you do. You might:

                - feel irritable, angry or tearful
                - feel worried, anxious or overwhelmed, or find it hard to make decisions
                - have headaches, stomach problems or tense muscles
                - feel dizzy or sick
                - eat more or less than usual
                - avoid people or things you used to enjoy

                ## Things that can help

                - **Talk to someone.** A partner, friend or family member can listen, and sometimes help in practical ways too.
                - **Break things into small steps.** Pick one small task at a time, and give yourself credit when it's done.
                - **Move a little.** A walk with the pram can help release tension.
                - **Plan ahead for busy days.** A short list or a bag packed the night before can take some pressure off.
                - **Notice small good things.** Some people find it helps to write down 3 things they are thankful for each day, however small.

                ## Asking for help

                If stress is affecting your daily life, or you are worried about how you feel, talk to your midwife or doctor. If you are in emotional crisis, you can call 800 70 2222 (free, 24 hours) or 116 123 (24 hours).
                """,
            EmergencyHeading: "Call 112 now if:",
            EmergencyItems:
            [
                "you feel unable to keep yourself or your baby safe"
            ],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "stress is affecting your daily life",
                "you feel stressed most of the time and it is not getting better",
                "you have chest pain that does not go away",
                "you are worried about how you feel"
            ],
            Sources:
            [
                new("NHS Every Mind Matters: Dealing with stress",
                    "https://www.nhs.uk/every-mind-matters/mental-health-issues/stress/"),
                new("Ministry of Health (gov.pl): Gdzie uzyskać pomoc psychologiczną i psychiatryczną",
                    "https://www.gov.pl/web/zdrowie/gdzie-uzyskac-pomoc-psychologiczna-i-psychiatryczna")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "feeling-lonely",
            Audience: GuidanceAudience.Mother,
            Category: EmotionalWellbeing,
            Topic: Topic.EmotionalSupport,
            AgeFrom: null,
            AgeTo: null,
            Title: "Feeling lonely",
            Summary: "Staying at home with a newborn can feel isolating. Some ways to feel more connected.",
            Body: """
                ## Why it happens

                Staying at home to look after a newborn is one of the life changes that can make people feel lonely. Your days may look very different from before.

                ## Ways to feel more connected

                - **Keep in touch.** Short, regular chats with friends and family can help, even a message or a call during a feed.
                - **Meet other new parents.** When you feel ready, a parent and baby group or a class is a good way to meet people going through the same thing.
                - **Get outside.** A walk, some fresh air, or a podcast while the baby naps can lift your mood.
                - **Do something for yourself.** A bath, a walk, or a coffee with a friend. Small things count.
                - **Say how you feel.** Telling someone you feel lonely can help. Try not to compare yourself with other parents.
                - **Invite someone.** Suggest a short walk or a free local event to someone you know.

                ## When to get help

                If loneliness goes on for a long time, it can affect your mood and raise the risk of stress, anxiety or depression. If you feel low most of the time, talk to your midwife or doctor.
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "you feel lonely or low most days and it is not getting better",
                "you have lost interest in things you usually enjoy",
                "you feel hopeless",
                "you are worried about how you feel"
            ],
            Sources:
            [
                new("NHS Every Mind Matters: Dealing with loneliness",
                    "https://www.nhs.uk/every-mind-matters/lifes-challenges/loneliness/"),
                new("Guy's and St Thomas' NHS: Looking after yourself",
                    "https://www.guysandstthomas.nhs.uk/our-services/maternity-after-your-baby-born/looking-after-yourself")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "coping-with-tiredness",
            Audience: GuidanceAudience.Mother,
            Category: SleepAndTiredness,
            Topic: Topic.PostpartumWellbeing,
            AgeFrom: null,
            AgeTo: null,
            Title: "Coping with tiredness",
            Summary: "Feeling exhausted is part of life with a new baby. Ways to get through the day with more energy.",
            Body: """
                ## It won't last forever

                Waking often at night with a baby is exhausting. But it does not last forever, and babies slowly start to sleep for longer stretches.

                ## During the day

                - **Rest when you can.** Even a short nap can help. If you worry about sleeping too long, set an alarm.
                - **Get some exercise.** Regular activity can make you feel less tired. A daily walk with the baby is an easy way to start.
                - **Take a few minutes to relax.** Even 5 to 10 minutes of deep relaxation may help.
                - **Eat and drink well.** Regular meals and plenty of water help you keep going.
                - **Look at what is causing stress.** If you can deal with the things that worry you, coping may get easier, even without more sleep.

                ## Let others help

                Ask for help where you can, for example with cooking, cleaning, or holding the baby while you rest. If you have a partner, share the work. If you are on your own, a friend or relative may be able to stay for a few days so you can catch up on sleep.

                ## When it is more than tiredness

                Sometimes tiredness is a sign of something else. Not being able to sleep even when your baby is asleep, feeling tired all the time, or feeling down or hopeless can be signs of postnatal depression.
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "you are very tired and struggling to cope",
                "you can't sleep even when your baby is asleep",
                "you feel down or hopeless, or have lost interest in things you usually enjoy"
            ],
            Sources:
            [
                new("NHS 111 Wales: Sleep and tiredness after having a baby",
                    "https://111.wales.nhs.uk/livewell/pregnancy/newmumssleep/"),
                new("Guy's and St Thomas' NHS: Looking after yourself",
                    "https://www.guysandstthomas.nhs.uk/our-services/maternity-after-your-baby-born/looking-after-yourself"),
                new("Tommy's: Feeling low after childbirth (baby blues)",
                    "https://tommys.org/pregnancy-information/im-pregnant/ask-a-midwife/feeling-low-after-childbirth-what-are-baby-blues")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "sleeping-with-a-new-baby",
            Audience: GuidanceAudience.Mother,
            Category: SleepAndTiredness,
            Topic: Topic.PostpartumWellbeing,
            AgeFrom: null,
            AgeTo: null,
            Title: "Sleeping with a new baby",
            Summary: "How to get a little more sleep for yourself when nights are broken.",
            Body: """
                ## Broken nights are normal

                Young babies wake at night because they need to feed. Your own sleep will be broken for a while. It gets easier as your baby starts to sleep for longer stretches.

                ## Getting more sleep

                - **Sleep when your baby sleeps.** Day naps help make up for lost night sleep.
                - **Go to bed early.** Try it for a week and see if it helps. If you find it hard to fall asleep, take half an hour to wind down first, for example with a warm bath.
                - **Share the nights.** If you have a partner, ask them to take some night feeds if your baby has formula, or to bring you the baby or expressed milk if you breastfeed. They could also do the early morning nappy change so you can sleep a bit longer.
                - **Ask for a few nights off.** If you are on your own, a friend or relative may be able to stay for a few days so you can sleep.

                ## Keep your baby safe when you are tired

                - Put your baby down in their own cot or Moses basket in the same room as you.
                - Never fall asleep with your baby on a sofa or in an armchair.
                - Don't share a bed with your baby if you are extremely tired, or if you or your partner smoke, have drunk alcohol or taken drugs.

                ## When sleep problems are a warning sign

                If you can't sleep even when your baby is asleep, or you feel tired all the time and low, talk to your midwife or doctor. These can be signs of postnatal depression.
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "you can't sleep even when your baby is asleep",
                "you feel tired all the time and low or hopeless",
                "you are struggling to cope"
            ],
            Sources:
            [
                new("NHS 111 Wales: Sleep and tiredness after having a baby",
                    "https://111.wales.nhs.uk/livewell/pregnancy/newmumssleep/"),
                new("NHS Start for Life: Safe sleep advice for babies",
                    "https://www.nhs.uk/start-for-life/baby/baby-basics/newborn-and-baby-sleeping-advice-for-parents/safe-sleep-advice-for-babies"),
                new("NHS: Reducing the risk of sudden infant death syndrome",
                    "https://www.nhs.uk/conditions/pregnancy-and-baby/reducing-risk-cot-death")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "looking-after-yourself",
            Audience: GuidanceAudience.Mother,
            Category: SelfCare,
            Topic: Topic.PostpartumWellbeing,
            AgeFrom: null,
            AgeTo: null,
            Title: "Looking after yourself",
            Summary: "Small, realistic ways to take care of your body and mind while you care for your baby.",
            Body: """
                ## Your needs count too

                Looking after your mind is just as important as looking after your body, but with a new baby it is easy to forget about yourself.

                ## The basics

                - **Rest.** Try to rest where you can. Even short naps can help.
                - **Eat and drink.** Regular meals and plenty of water help you recover and keep your energy up.
                - **Move a little.** Short walks are a gentle way to get fresh air and some exercise.

                ## Accept help

                Ask for help where you can: cooking, cleaning, shopping, or watching the baby while you rest. Take some pressure off yourself, and give yourself credit for what you are already doing.

                ## Time for you

                Try to do one thing just for yourself, even if it is small: a bath, a walk, or a coffee with a friend. When you feel ready, get out of the house and meet other new parents.

                ## Keep an eye on your mood

                Feeling low, tearful or anxious for a few days after birth is common. If it lasts more than 2 weeks or gets worse, talk to your midwife or doctor.
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "low mood lasts more than 2 weeks or gets worse",
                "you are struggling to cope",
                "you are worried about how you feel"
            ],
            Sources:
            [
                new("Guy's and St Thomas' NHS: Looking after yourself",
                    "https://www.guysandstthomas.nhs.uk/our-services/maternity-after-your-baby-born/looking-after-yourself"),
                new("Tommy's: Feeling low after childbirth (baby blues)",
                    "https://tommys.org/pregnancy-information/im-pregnant/ask-a-midwife/feeling-low-after-childbirth-what-are-baby-blues"),
                new("NHS Start for Life: Your mental health",
                    "https://www.nhs.uk/start-for-life/baby/your-mental-health/")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "eating-and-drinking-well",
            Audience: GuidanceAudience.Mother,
            Category: Nutrition,
            Topic: Topic.Nutrition,
            AgeFrom: null,
            AgeTo: null,
            Title: "Eating and drinking well",
            Summary: "A simple guide to food and drinks after birth, including while breastfeeding.",
            Body: """
                ## No special diet needed

                You don't need a special diet after birth or while breastfeeding. A varied, healthy diet is enough:

                - at least 5 portions of different fruit and vegetables a day (fruit juice or smoothies count as one portion at most, about 150ml)
                - starchy foods such as bread, potatoes, rice or pasta, wholegrain where you can
                - protein such as beans, lentils, eggs, fish and meat
                - milk and dairy foods, or alternatives with added calcium

                ## Drinks

                Keep a drink close by when you feed your baby. Water, low-fat milk and juice diluted with water are good choices.

                ## Fish

                Have at least 2 portions of fish a week, one of them oily, such as salmon or mackerel. A portion is about 140g. If you breastfeed, have no more than 2 portions of oily fish a week, and no more than 1 portion a week of shark, swordfish or marlin.

                ## Caffeine

                If you breastfeed, keep caffeine to no more than 200mg a day, and try to cut down more while your baby is under 6 months. As a rough guide from the NHS, a coffee has about 90mg, a tea about 55mg, a cola about 37mg and a 250ml energy drink about 80mg.

                ## Vegan or restricted diets

                If you don't eat animal foods, choose dairy alternatives with added calcium, vitamin D and iodine. You may also need foods with added vitamin B12 or a B12 supplement. Ask your doctor or pharmacist.

                ## Vitamins

                Ask your doctor or pharmacist whether you need a vitamin D supplement, especially in autumn and winter.
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "you find it hard to eat enough",
                "you follow a restricted diet and are unsure if you get the nutrients you need",
                "you have questions about food allergies"
            ],
            Sources:
            [
                new("NHS: Breastfeeding and diet",
                    "https://www.nhs.uk/baby/breastfeeding-and-bottle-feeding/breastfeeding-and-lifestyle/diet/")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "baby-feeding-routine",
            Audience: GuidanceAudience.Baby,
            Category: Feeding,
            Topic: Topic.BabyFeeding,
            AgeFrom: null,
            AgeTo: null,
            Title: "Baby feeding routine",
            Summary: "How often young babies feed, how to read hunger signs, and how to tell your baby is getting enough.",
            Body: """
                ## How often

                In the first days, babies often want to feed every 1 to 3 hours. In the first few weeks, expect at least 8 to 12 feeds in 24 hours, sometimes more. After the first days, feeds usually become fewer but longer.

                ## Follow your baby's lead

                Feeding your baby when they show they are hungry, for as long as they want, is called responsive feeding. You can't overfeed a breastfed baby. Breastfeeding is also fine when your breasts feel full, or just to comfort your baby.

                Early hunger signs:

                - getting restless
                - sucking fists or fingers
                - making murmuring sounds
                - turning their head with their mouth open

                Try to feed before your baby cries. A crying baby is harder to feed.

                ## Bottle feeding

                Bottle feeds can be responsive too. Feed when your baby shows hunger signs, and take breaks during the feed. Your baby does not need to finish the bottle every time. Signs they want a break include turning away from the teat, pushing it out with their tongue, or letting milk dribble out.

                ## Is my baby getting enough?

                - **Wet nappies:** 2 or 3 in the first 2 days, then from day 5 at least 6 heavy wet nappies every 24 hours.
                - **Poo:** from day 4, at least 2 soft yellow poos a day in the first weeks.
                - **Weight:** most babies lose some weight in the first 3 to 4 days, then gain steadily.
                - **During feeds:** you can see and hear your baby swallow, and they seem calm and content after most feeds.
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
                "you are worried your baby is not getting enough milk"
            ],
            Sources:
            [
                new("NHS: Breastfeeding: the first few days",
                    "https://www.nhs.uk/conditions/baby/breastfeeding-and-bottle-feeding/breastfeeding/the-first-few-days"),
                new("NHS: Is my baby getting enough milk?",
                    "https://nhs.uk/baby/breastfeeding-and-bottle-feeding/breastfeeding-problems/enough-milk"),
                new("Cambridge University Hospitals NHS: Bottle feeding",
                    "https://www.cuh.nhs.uk/rosie-hospital/maternity/infant-feeding/bottle-feeding/"),
                new(CuhSignsName, CuhSignsUrl)
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "crying-and-colic",
            Audience: GuidanceAudience.Baby,
            Category: Crying,
            Topic: Topic.BabySleep,
            AgeFrom: null,
            AgeTo: null,
            Title: "Crying and colic",
            Summary: "Why babies cry so much in the first months, what colic is, and how to cope.",
            Body: """
                ## Crying is normal

                All babies cry, and it will stop. Crying usually increases from about 2 weeks old, is at its peak at around 6 to 8 weeks, and then slowly gets less. Ways of calming your baby work sometimes, but not always. A baby who is hard to settle does not mean you are doing something wrong.

                ## What is colic?

                Colic is when a baby cries a lot but there is no clear reason. A baby may have colic if they cry for more than 3 hours a day, 3 days a week, for at least a week, and are otherwise well. The crying is often worse in the afternoon and evening.

                During crying, a baby with colic may:

                - be hard to comfort
                - clench their fists
                - go red in the face
                - pull their knees up or arch their back
                - have a rumbling tummy or wind

                Colic can start when a baby is a few weeks old, and it usually stops by 3 to 4 months. Anti-colic drops and herbal or probiotic products are sold, but there is no good evidence that they help.

                ## If you feel you can't cope

                Constant crying is hard. If you feel overwhelmed, put your baby down somewhere safe, such as their cot, and leave the room for a few minutes to calm down. Then go back and check on them.

                Never shake a baby. Shaking can cause brain damage or death.
                """,
            EmergencyHeading: "Call 112 now if your baby:",
            EmergencyItems:
            [
                "has a weak or high-pitched cry",
                "has a cry that sounds very different from their normal cry",
                "is very sleepy, limp or floppy, or does not respond to you"
            ],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "you are worried about your baby's crying",
                "nothing you try seems to help",
                "you are struggling to cope",
                "your baby is not growing or gaining weight as expected",
                "the crying still goes on after 4 months"
            ],
            Sources:
            [
                new("NHS: Colic",
                    "https://www.nhs.uk/content-widgets/health-a-to-z/colic"),
                new("National Centre for Infant Crying (ICON): Babies cry, you can cope",
                    "https://ncic.nhs.uk/news/babies-cry-you-can-cope"),
                new(CuhSignsName, CuhSignsUrl)
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "a-day-with-your-baby",
            Audience: GuidanceAudience.Baby,
            Category: DailyLife,
            Topic: Topic.BabySleep,
            AgeFrom: null,
            AgeTo: null,
            Title: "A day with your baby",
            Summary: "How to organize your days in the first months, without a strict schedule.",
            Body: """
                ## No strict schedule yet

                The first months are for getting to know your baby. This is not the time to worry about a fixed routine. A loose, flexible pattern can still help you feel more in control.

                ## A simple pattern

                Many parents find this order works well:

                1. Feed your baby when they wake.
                2. Change their nappy.
                3. Play or have a cuddle.
                4. Settle them to sleep when they look tired.

                Follow your baby's lead. If they seem hungry again after play, feed them before sleep.

                ## Tired signs

                Watch your baby rather than the clock. Some newborns get tired after about 1 to 1½ hours awake, and playtime may last only 10 to 20 minutes. Sometimes a baby is ready to sleep straight after a feed.

                ## Day and night

                Help your baby learn that night is for sleeping. In the day, open the curtains and play, and don't worry about normal household noise. At night, keep the lights low and your voice quiet, and put your baby down soon after a feed and a nappy change.

                ## Making it work for you

                - Ask for help with cooking, cleaning or shopping, and share the work with your partner if you have one.
                - Rest when you can, even for a short nap.
                - As your baby grows, things done in the same order at about the same time each day can make the day more predictable for both of you. Expect changes with growth spurts, teething or illness.
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your midwife or doctor if:",
            GetHelpItems:
            [
                "you are finding it very hard to cope",
                "your baby is very sleepy and feeding poorly",
                "you are worried about your baby"
            ],
            Sources:
            [
                new("Raising Children Network: Newborn sleep routines",
                    "https://raisingchildren.net.au/newborns/sleep/settling-routines/newborn-sleep-routines"),
                new("Raising Children Network: Family routines",
                    "https://raisingchildren.net.au/babies/family-life/routines-rituals/family-routines"),
                new("NHS: Helping your baby to sleep",
                    "https://www.nhs.uk/baby/caring-for-a-newborn/helping-your-baby-to-sleep/"),
                new("Guy's and St Thomas' NHS: Looking after yourself",
                    "https://www.guysandstthomas.nhs.uk/our-services/maternity-after-your-baby-born/looking-after-yourself"),
                new(CuhSignsName, CuhSignsUrl)
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "baby-nutrition-0-to-3-months",
            Audience: GuidanceAudience.Baby,
            Category: NutritionByAge,
            Topic: Topic.BabyFeeding,
            AgeFrom: 0,
            AgeTo: 3,
            Title: "Baby nutrition: 0 to 3 months",
            Summary: "Breast milk or first infant formula is all your baby needs. How to make up formula safely.",
            Body: """
                ## Milk only

                The WHO recommends breast milk only for the first 6 months, with no other food or drink, not even water. Feed your baby when they show they are hungry, day and night.

                ## If you use formula

                - Use first infant formula. Your baby does not need any other type of formula in the first year.
                - Feed responsively: let your baby take breaks, and don't make them finish the bottle.

                ## Making up a formula feed

                1. Fill the kettle with fresh tap water. Don't use water that was boiled before.
                2. Boil the water, then let it cool in the kettle for no more than 30 minutes, so it is still at least 70°C.
                3. Pour the right amount of water into a clean, sterilised bottle first.
                4. Add the exact number of scoops of powder from the pack, levelled off. Don't add extra.
                5. Cool the bottle under cold running water, then check the temperature on the inside of your wrist. It should feel warm or cool, not hot.
                6. Throw away any milk left over after the feed.

                ## Vitamin D

                Babies who are breastfed usually need a vitamin D supplement from birth. Babies who drink about 500ml of formula a day or more usually don't, because formula already contains vitamin D. Ask your midwife or your baby's doctor which drops to use.
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
                "you are worried your baby is not getting enough milk"
            ],
            Sources:
            [
                new("WHO: Breastfeeding",
                    "https://www.who.int/westernpacific/health-topics/breastfeeding/1"),
                new("Cambridge University Hospitals NHS: Bottle feeding",
                    "https://www.cuh.nhs.uk/rosie-hospital/maternity/infant-feeding/bottle-feeding/"),
                new("NHS Start for Life: How to make up a feed",
                    "https://www.nhs.uk/start-for-life/baby/feeding-your-baby/bottle-feeding/how-to-make-up-a-feed"),
                new("NHS Start for Life: Vitamins for babies",
                    "https://www.nhs.uk/best-start-in-life/baby/baby-basics/caring-for-your-baby/vitamins-for-babies/"),
                new(CuhSignsName, CuhSignsUrl)
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "baby-nutrition-6-to-9-months",
            Audience: GuidanceAudience.Baby,
            Category: NutritionByAge,
            Topic: Topic.Nutrition,
            AgeFrom: 6,
            AgeTo: 9,
            Title: "Baby nutrition: 6 to 9 months",
            Summary: "Starting solid food: textures, finger foods, drinks, and what to avoid.",
            Body: """
                ## Milk is still the main drink

                Breast milk or first infant formula stays your baby's main drink until 12 months. As your baby eats more food, they may want a little less milk. Formula-fed babies may need about 600ml a day, but this is only a guide. Give milk feeds after meals, and don't make your baby finish the bottle.

                ## Moving on with food

                - Over these months, your baby will slowly move towards 3 meals a day.
                - Move on to mashed and lumpy food and finger foods as soon as your baby can manage them.
                - Finger foods help your baby learn to feed themselves and to chew. Cook harder vegetables and fruit until soft.
                - Always stay with your baby while they eat.
                - Offer a mix of foods: vegetables and fruit, starchy foods, and protein such as meat, fish without bones, eggs, beans and lentils.
                - It can take 10 tries or more before a baby accepts a new food. Keep offering it.

                ## Drinks

                Offer sips of water from an open cup with meals. Avoid juice, squash, fizzy drinks and milkshakes. Cow's milk can be used in cooking from 6 months, but not as a drink until 12 months.

                ## What to avoid

                - **Salt:** don't add it to food or cooking water. Avoid stock cubes and gravy.
                - **Sugar:** don't add it. Sweet pouches should not be everyday food, and don't let your baby suck straight from a pouch.
                - **Honey:** not until after 1 year.
                - **Whole nuts:** not until 5 years. Ground nuts or smooth nut butter spread on food are fine.
                - **Rice drinks:** not as a replacement for milk.
                - **Snacks:** not needed before 12 months. Offer an extra milk feed if your baby seems hungry between meals.

                If food allergies run in your family, talk to your baby's doctor before you give nuts or other common allergy foods.
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your baby's doctor if:",
            GetHelpItems:
            [
                "food allergies run in your family and you are starting new foods",
                "you are worried about how your baby is eating or gaining weight",
                "your baby was born early and you are unsure about starting food"
            ],
            Sources:
            [
                new("NHS Start for Life: What to feed your baby, 7 to 9 months",
                    "https://www.nhs.uk/best-start-in-life/baby/weaning/what-to-feed-your-baby/7-to-9-months/"),
                new("NHS Start for Life: How to start weaning your baby",
                    "https://www.nhs.uk/start-for-life/baby/weaning/how-to-start-weaning-your-baby/"),
                new("NHS: Foods to avoid giving babies and young children",
                    "https://www.nhs.uk/baby/weaning-and-feeding/foods-to-avoid-giving-babies-and-young-children/")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov),

        new SeedPage(
            Slug: "baby-nutrition-9-to-12-months",
            Audience: GuidanceAudience.Baby,
            Category: NutritionByAge,
            Topic: Topic.Nutrition,
            AgeFrom: 9,
            AgeTo: 12,
            Title: "Baby nutrition: 9 to 12 months",
            Summary: "Three meals a day, more finger foods, and getting ready for the first birthday.",
            Body: """
                ## Three meals a day

                By now your baby should be having 3 meals a day: breakfast, lunch and dinner. Lunch and dinner can have a main course and a dessert, such as fruit or plain yoghurt. Babies under 12 months don't need snacks.

                ## Textures and finger foods

                Your baby can now manage mashed, lumpy and chopped food and finger foods. Offer bigger soft pieces and more finger foods, so they can practise picking up food and feeding themselves. Stay with your baby while they eat.

                ## Milk

                Breast milk or first infant formula is still the main drink until 12 months. Many babies now have about 3 milk feeds a day. Formula-fed babies may need about 400ml a day, but this is only a guide. You can keep breastfeeding for as long as you both want.

                ## Drinks

                Your baby can sip water from a cup when thirsty. Avoid juice, squash, fizzy drinks, milkshakes and baby or toddler drinks, because they can cause tooth decay. Full-fat cow's milk can be used in cooking, but not as a drink until 12 months.

                ## Still to avoid

                - no added salt or sugar
                - no honey until after 1 year
                - no whole nuts until 5 years
                - jars and pouches are fine now and then, but not as everyday food, and squeeze them onto a spoon instead of letting your baby suck from the pouch
                """,
            EmergencyHeading: null,
            EmergencyItems: [],
            GetHelpHeading: "Talk to your baby's doctor if:",
            GetHelpItems:
            [
                "you are worried about how your baby is eating or gaining weight",
                "your baby struggles with lumpy or chopped food",
                "you have questions about food allergies"
            ],
            Sources:
            [
                new("NHS Start for Life: What to feed your baby, 10 to 12 months",
                    "https://www.nhs.uk/start-for-life/baby/weaning/what-to-feed-your-baby/10-to-12-months/"),
                new("NHS Start for Life: What to feed your baby, 7 to 9 months",
                    "https://www.nhs.uk/best-start-in-life/baby/weaning/what-to-feed-your-baby/7-to-9-months/"),
                new("NHS: Foods to avoid giving babies and young children",
                    "https://www.nhs.uk/baby/weaning-and-feeding/foods-to-avoid-giving-babies-and-young-children/")
            ],
            LastChecked: Checked09Nov,
            NextCheckOn: NextCheck09Nov)
    ];
}
