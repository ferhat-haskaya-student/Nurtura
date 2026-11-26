using Nurtura.Core.Entities;

namespace Nurtura.Core.Demo;

// What the demo mothers and professionals wrote. This is user content, not interface text, so it is not in the
// language files. Plain and short; no diagnosis, no medicine names or doses, no medical advice in the forum.
internal static class DemoTexts
{
    // Check-in notes, by how the day felt.
    public static readonly string[] CheckInLow =
    [
        "Rough night. Running on coffee and toast.",
        "Cried in the shower this morning. Felt a bit better after.",
        "Everything takes so long. Didn't get dressed until lunch.",
        "Was short with everyone today and felt bad about it after.",
        "So tired my eyes hurt.",
        "Hard day. Only settled when I held her.",
        "Didn't leave the flat today.",
        "Felt like I was getting nothing right."
    ];

    public static readonly string[] CheckInMid =
    [
        "Okay day. Short walk to the shop.",
        "Mum came over so I napped for an hour.",
        "Tired but fine.",
        "Lots of feeding, not much else.",
        "Grey day, stayed in with tea.",
        "Did one load of washing. Counting it as a win.",
        "Bit of a blur, but we got through it.",
        "Long phone call with my sister, that was nice."
    ];

    public static readonly string[] CheckInHigh =
    [
        "Good day! Long walk in the park.",
        "Big stretch of sleep last night, I feel almost human.",
        "Met a friend for coffee with the pram.",
        "Cooked a proper dinner for the first time in weeks.",
        "Lovely morning, lots of smiles.",
        "Felt like myself for a few hours.",
        "Sunny walk, then a nap together."
    ];

    public static readonly string[] CheckInBadNight =
    [
        "Up every hour after 2. Feel like a zombie.",
        "Bad night, nobody slept much.",
        "Long night. Took ages to settle after the 3am wake."
    ];

    // A few lines only one mother would write.
    public static readonly string[] CheckInMarta =
    [
        "Toddler meltdown at the shop, baby slept through it.",
        "Both kids napped at the same time. A small miracle.",
        "Nursery run in the rain with the pram. Never again."
    ];

    public static readonly string[] CheckInOla =
    [
        "Partner away again, long evening.",
        "Quiet day, talked to nobody but the baby.",
        "Went to the baby group at the library. Glad I went.",
        "Video call with my mum, she says Lena looks just like me."
    ];

    public static readonly string[] SleepNotes =
    [
        "Fell asleep in the pram.",
        "Contact nap on me.",
        "Took ages to settle.",
        "Slept in the car on the way to Grandma's.",
        "Woke up happy.",
        "Short one, noisy neighbours."
    ];

    public static readonly string[] BottleNotes =
    [
        "Daddy's feed.",
        "Fell asleep halfway, finished after a burp.",
        "Fussy, took a few breaks.",
        "Fed on the go in the park.",
        "Very hungry this time."
    ];

    public static readonly string[] BreastNotes =
    [
        "Distracted, kept looking around.",
        "Fed in the café.",
        "Quick snack before the nap.",
        "Lots of feeds this evening.",
        "Long feed, we both dozed off."
    ];

    public static readonly string[] Solids =
    [
        "Carrot puree, a few spoons.",
        "Mashed banana, loved it.",
        "Baby porridge, most of it on his face.",
        "Sweet potato, made a funny face.",
        "Pear puree, ate it all.",
        "Broccoli. Not a fan.",
        "Avocado mashed with a fork.",
        "Apple and pear, half a bowl.",
        "Pumpkin puree, wanted more.",
        "Porridge with banana.",
        "Courgette, a few spoons.",
        "Toast fingers, mostly played with them."
    ];

    // "What would you like to talk about?"
    public const string ConcernKasiaFeeding =
        "Zosia has expressed milk and one formula bottle at night. I'd like to check how I'm doing with pumping " +
        "and the bottles, and how to share night feeds with my husband.";

    public const string ConcernKasiaRecovery =
        "Five weeks after birth. I still feel sore after longer walks and get tired very quickly. I'd like to talk " +
        "about recovery and what I can slowly start doing again.";

    public const string ConcernKasiaNaps =
        "She fights her naps and mostly sleeps on me in the day. I'd like some ideas for daytime sleep.";

    public const string ConcernKasiaFeelings =
        "I'm happy with Zosia but some days I feel flat and not like myself. I'd like to talk to someone about it.";

    public const string ConcernKasiaRoutine =
        "We have a routine now, 19:00 to 07:00 with one feed around 1am. Some naps are only 30 minutes. I'd like " +
        "to talk about naps and what the next months might look like.";

    public const string ConcernMartaSolids =
        "Antek is almost 6 months. I'm still breastfeeding and want to start solids. Looking for a simple plan for " +
        "the first weeks.";

    public const string ConcernMartaEnergy =
        "Two small kids and I'm running on empty. I'd like to talk about getting a bit of energy back and some " +
        "time for myself.";

    public const string ConcernOlaLonely =
        "My partner works long hours and I'm alone with Lena most days. I feel lonely and worried a lot. I'd like " +
        "to talk to someone.";

    public const string ConcernOlaBedtime =
        "Lena only falls asleep feeding and wakes when I put her down. Evenings are long. I'd like help with a " +
        "bedtime routine.";

    public const string ConcernOlaFeeding =
        "I breastfeed in the day and give formula bottles at night. I'd like to talk about combining both and " +
        "whether my plan makes sense.";

    public const string RejectMessage =
        "Sorry, I can't take this hour after all. I've opened more times next week, please book one of those.";

    // Chats: (who, text), in order.
    public static readonly (AppointmentParty From, string Text)[] ChatKasiaFeeding =
    [
        (AppointmentParty.Professional, "Hi Kasia, nice to meet you. I read your note. How are feeds going this week?"),
        (AppointmentParty.Mother, "Hi! Mostly okay. I pump about every 3 hours and she takes the bottle well."),
        (AppointmentParty.Mother, "I just worry I'm doing the formula part wrong."),
        (AppointmentParty.Professional, "What does a normal day look like? Which feed is formula?"),
        (AppointmentParty.Mother, "Usually the night one, so my husband can do it and I can sleep a few hours."),
        (AppointmentParty.Professional, "That sounds like a good plan for your family. Sharing the night feed helps a lot of mums."),
        (AppointmentParty.Professional, "Let's talk about fitting pumping around your day so it doesn't take over."),
        (AppointmentParty.Mother, "Yes please, I feel like I'm always attached to the pump."),
        (AppointmentParty.Professional, "I'll put the main points in a note for you after we finish."),
        (AppointmentParty.Mother, "Thank you, this helps a lot.")
    ];

    public static readonly (AppointmentParty From, string Text)[] ChatKasiaRecovery =
    [
        (AppointmentParty.Professional, "Hello Kasia. How are you feeling, five weeks on?"),
        (AppointmentParty.Mother, "Better than at the start, but I still feel sore after a longer walk."),
        (AppointmentParty.Professional, "Thanks for telling me. How long are your walks now?"),
        (AppointmentParty.Mother, "About 30 minutes. After that I need to sit down."),
        (AppointmentParty.Professional, "Building up slowly is a good way to go. Do you get any rest in the day?"),
        (AppointmentParty.Mother, "When she naps, sometimes. My mum helps twice a week."),
        (AppointmentParty.Professional, "Good. Please mention the soreness at your check-up too, so the doctor can take a look."),
        (AppointmentParty.Mother, "I will. Thank you."),
        (AppointmentParty.Professional, "I think we've covered everything, so I'll end the session here. I'll write you a short note.")
    ];

    public static readonly (AppointmentParty From, string Text)[] ChatKasiaFeelings =
    [
        (AppointmentParty.Professional, "Hi Kasia, I'm glad you booked. How have you been feeling lately?"),
        (AppointmentParty.Mother, "Hard to explain. I love her so much but some days I feel flat."),
        (AppointmentParty.Professional, "Thank you for saying that. When do the flat days happen most?"),
        (AppointmentParty.Mother, "When my husband is at work and the days all look the same."),
        (AppointmentParty.Professional, "That makes sense. What did you enjoy before Zosia was born?"),
        (AppointmentParty.Mother, "Running, and meeting friends for coffee."),
        (AppointmentParty.Professional, "Could one small thing like that fit into your week now?"),
        (AppointmentParty.Mother, "Maybe a coffee on Saturday while he takes her for a walk."),
        (AppointmentParty.Professional, "That sounds like a good start. How does it feel to say it out loud?"),
        (AppointmentParty.Mother, "Lighter, honestly.")
    ];

    public static readonly (AppointmentParty From, string Text)[] ChatMartaSolids =
    [
        (AppointmentParty.Professional, "Hi Marta! So Antek is almost 6 months. How is he at mealtimes?"),
        (AppointmentParty.Mother, "He grabs everything from my plate, so I think he wants to try."),
        (AppointmentParty.Professional, "Lovely. Are you planning to keep breastfeeding alongside?"),
        (AppointmentParty.Mother, "Yes, I'd like to. It worked well with my first."),
        (AppointmentParty.Professional, "Good. Start with one small meal a day, at a time that suits you both."),
        (AppointmentParty.Mother, "Lunch would be easiest, his sister is at nursery then."),
        (AppointmentParty.Professional, "Perfect. Offer a few spoons and let him lead. Some days he'll eat more, some days less."),
        (AppointmentParty.Mother, "Sounds doable. That's all I needed, thank you! I'll end it here, he's waking up.")
    ];

    public static readonly (AppointmentParty From, string Text)[] ChatOlaLonely =
    [
        (AppointmentParty.Professional, "Hi Ola, thank you for booking. How are you today?"),
        (AppointmentParty.Mother, "Tired. Honestly I almost cancelled."),
        (AppointmentParty.Professional, "I'm glad you didn't. You wrote that you feel lonely. Can you tell me more?"),
        (AppointmentParty.Mother, "My partner leaves at 7 and comes back after 8. I talk to nobody all day except Lena."),
        (AppointmentParty.Professional, "That's a long day on your own. Is there anyone nearby you could see?"),
        (AppointmentParty.Mother, "There's a baby group at the library on Thursdays. I never went."),
        (AppointmentParty.Professional, "How would it feel to try it once, just for half an hour?"),
        (AppointmentParty.Mother, "Scary but maybe good."),
        (AppointmentParty.Professional, "We can talk about how it went next time, if you like."),
        (AppointmentParty.Mother, "Okay. Thank you for listening.")
    ];

    // Closing notes.
    public const string NoteKasiaFeeding =
        "Thank you for the chat, Kasia. We talked about how feeds are going, fitting pumping around your day and " +
        "sharing the night feed with your husband. You're already doing a lot. What we agreed: pump at times that " +
        "fit your day rather than the clock, and keep a short list of questions for next time. If anything about " +
        "Zosia's feeding worries you, contact your midwife or doctor.";

    public const string NoteKasiaRecovery =
        "Thanks for talking with me today, Kasia. We went through how you feel five weeks after the birth. Keep " +
        "building your walks up slowly, rest when Zosia naps, and let your mum help where she can. Please mention " +
        "the soreness at your check-up so your doctor can take a look. Book again any time you'd like to talk.";

    public const string NoteKasiaFeelings =
        "Thank you for sharing so openly, Kasia. We talked about the flat days, what makes them harder and what " +
        "you enjoyed before Zosia. You planned a Saturday coffee with a friend while your husband takes Zosia out. " +
        "Notice how the week feels after it. If the low days get more frequent or heavier, talk to your doctor, " +
        "and you're welcome to book with me again.";

    public const string NoteMartaSolids =
        "Nice to meet you and Antek, Marta. We talked about starting solids at lunchtime while you keep " +
        "breastfeeding. Start with a few spoons of soft food and let him lead how much he eats. It's fine if some " +
        "days he eats very little. Book again if you'd like to talk about the next steps.";

    public const string NoteOlaLonely =
        "Thank you for talking with me, Ola. We talked about the long days at home on your own and how lonely they " +
        "feel. You thought about trying the Thursday baby group at the library, just for half an hour to start. " +
        "It's also okay to tell your partner how the days feel. If the worry gets heavier or you ever feel " +
        "unsafe, contact your doctor, or call 116 123 at any time.";

    // Forum: posts with their comments. Day is the post's day counted from the first day of the 3 months
    // (negative: days before today); comments are hours after the post.
    internal sealed record PostText(
        string Author, int Day, int Hour, int Minute, int CategoryId, string Title, string Body,
        (string Author, int Hours, string Body)[] Comments);

    public const int CategoryEmotionalWellbeing = 1;
    public const int CategorySleepAndTiredness = 2;
    public const int CategoryBabyFeeding = 3;
    public const int CategoryBabySleep = 4;
    public const int CategoryNutrition = 5;
    public const int CategoryRecoveryAfterBirth = 6;
    public const int CategoryGeneralSupport = 7;
    public const int CategoryConsultationExperiences = 8;

    // The comment Ola reports as unkind.
    public const string UnkindComment = "Everyone is tired. Posting about it won't make the nights shorter.";

    public const string FourAmTitle = "How do you get through the 4am feed?";

    public static readonly PostText[] Posts =
    [
        new(DemoAccounts.KasiaEmail, 11, 14, 20, CategoryBabyFeeding,
            "Expressed milk and formula, how do you organise it?",
            "We do mostly expressed milk with one formula bottle at night, so my husband can take that feed and I " +
            "get a few hours of sleep. I felt guilty about it at first. Now it just feels like what works for us.\n\n" +
            "How do you all organise your day around pumping?",
            [
                (DemoAccounts.OlaEmail, 5, "We do formula at night too. I felt guilty at first as well. Nice to read I'm not the only one."),
                (DemoAccounts.MartaEmail, 26, "With my first I pumped too. A podcast and a big glass of water made the sessions go faster for me.")
            ]),
        new(DemoAccounts.OlaEmail, 16, 21, 40, CategoryEmotionalWellbeing,
            "Does anyone else feel lonely in the evenings?",
            "My partner works late most days, and once Lena is asleep the flat is so quiet. I love her to bits, " +
            "but some evenings I just sit on the sofa and feel a bit lost. Is it just me?",
            [
                (DemoAccounts.KasiaEmail, 3, "Not just you. Evenings are the hardest for me too. I message my sister every night just to talk to a grown-up."),
                (DemoAccounts.MartaEmail, 9, "With my first I felt exactly like this. Getting out once a day, even just to the shop, helped me a bit."),
                (DemoAccounts.OlaEmail, 20, "Thank you both, it helps to know I'm not the only one.")
            ]),
        new(DemoAccounts.MartaEmail, 29, 13, 5, CategoryBabySleep,
            "Second baby, a completely different sleeper",
            "My first slept anywhere, any time. Antek only naps in the pram if it's moving! I'd forgotten how much " +
            "of the day goes on getting a baby to sleep. Anyone else with a second baby who is the total opposite " +
            "of the first?",
            [
                (DemoAccounts.KasiaEmail, 4, "Ha, Zosia is a pram napper too. We do a lot of laps around the block."),
                (DemoAccounts.OlaEmail, 24, "Lena only sleeps on me in the day. I've watched a whole series this month.")
            ]),
        new(DemoAccounts.KasiaEmail, 44, 18, 30, CategoryRecoveryAfterBirth,
            "First walk longer than 30 minutes!",
            "Six weeks in and today we walked to the river and back. It took forever and I sat on every bench, " +
            "but I did it. Small thing, but I'm proud.\n\nWhat was your first \"I'm getting there\" moment?",
            [
                (DemoAccounts.OlaEmail, 2, "Well done! Mine was the first time I went to the shop alone with the pram."),
                (DemoAccounts.MartaEmail, 25, "That's a big one. Mine was making it to a friend's birthday, even if I left early.")
            ]),
        new(DemoAccounts.OlaEmail, 50, 20, 15, CategoryConsultationExperiences,
            "My first online consultation",
            "I almost cancelled because I was so nervous, but I'm glad I didn't. It was just a chat, the " +
            "psychologist was really kind and didn't rush me. If you're thinking about booking but feel silly " +
            "about it, I felt the same.",
            [
                (DemoAccounts.KasiaEmail, 5, "Same for me, I was nervous before my first one too. It was easier than I thought."),
                (DemoAccounts.MartaEmail, 20, "Thanks for sharing this. I've been thinking about booking for a while.")
            ]),
        new(DemoAccounts.OlaEmail, 53, 4, 12, CategorySleepAndTiredness,
            FourAmTitle,
            "It's the 4am one that gets me. Everyone is asleep, it's dark and I feel like the only person awake in " +
            "the city. What do you do to stay awake and not feel so alone at that hour?",
            [
                (DemoAccounts.KasiaEmail, 6, "I put a podcast on very low and get water and a snack ready before I go to bed. Makes it feel less lonely somehow."),
                (DemoAccounts.MartaEmail, 10, UnkindComment),
                (DemoAccounts.OlaEmail, 14, "Thanks Kasia, I'll try the podcast.")
            ]),
        new(DemoAccounts.MartaEmail, -20, 13, 45, CategoryNutrition,
            "What do you eat when you only have one hand free?",
            "With a baby on me and a toddler at my feet, lunch is usually crackers standing up. I'm trying to do " +
            "better. What are your quick one-hand meals?\n\nMine so far: wraps, yoghurt with oats, and leftover " +
            "pasta straight from the box.",
            [
                (DemoAccounts.KasiaEmail, 3, "Overnight oats! I make them in the evening and eat them with one hand."),
                (DemoAccounts.OlaEmail, 7, "Banana and peanut butter on toast, every single day."),
                (DemoAccounts.MartaEmail, 9, "Overnight oats is a great idea, adding it to the list.")
            ]),
        new(DemoAccounts.OlaEmail, -3, 20, 30, CategoryGeneralSupport,
            "Small wins this week",
            "Let's share some small wins. Mine: I went to the baby group at the library again, and Lena slept 4 " +
            "hours in a row the other night. What's yours?",
            [
                (DemoAccounts.KasiaEmail, 6, "Zosia slept from 7 to 1 last night! And I had a hot coffee."),
                (DemoAccounts.MartaEmail, 40, "I got both kids to nap at the same time and had 20 minutes of quiet."),
                (DemoAccounts.OlaEmail, 44, "Love these, thank you both.")
            ])
    ];
}
