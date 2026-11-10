using Nurtura.Core.Entities;

namespace Nurtura.Core.Content;

// The Nurtura Assistant keywords, copied exactly from docs/nurtura_guidance_content.md,
// section "Nurtura Assistant keywords" (9 Nov 2026).
// Page keywords are seeded into ContentKeywordTag by AssistantKeywordSeeder. Topic keywords and urgent words
// are fixed lists, so they stay in code.
public static class AssistantKeywords
{
    public static readonly IReadOnlyDictionary<string, string[]> Pages = new Dictionary<string, string[]>
    {
        ["when-to-seek-professional-support"] =
        [
            "help", "worried", "warning", "signs", "urgent", "emergency", "doctor", "midwife", "depression", "fever",
            "pain", "bleed", "pomoc", "martwi", "niepokoj", "objaw", "lekarz", "położn", "depresj", "gorączk", "ból",
            "bólu", "boli", "krwaw"
        ],
        ["baby-sleep-routine"] =
        [
            "sleep", "nap", "naps", "night", "wake", "bedtime", "cot", "settle", "routine", "sids", "sen", "snu",
            "śnie", "spać", "śpi", "śpią", "drzemk", "noc", "nocy", "nocą", "budzi", "łóżeczk", "usypia", "rytuał"
        ],
        ["baby-nutrition-3-to-6-months"] =
        [
            "solid", "weaning", "ready", "food", "puree", "four months", "readiness", "milk", "stałe", "rozszerzan",
            "gotow", "jedzen", "papk", "mleko"
        ],
        ["recovery-after-birth"] =
        [
            "recovery", "bleed", "stitch", "tear", "caesarean", "c-section", "wound", "scar", "pelvic", "piles",
            "constipation", "sore", "breast", "połóg", "połogu", "krwaw", "szwy", "szwów", "pęknię", "cesarsk",
            "cięci", "rana", "rany", "blizn", "miednic", "hemoroid", "zaparci", "piersi"
        ],
        ["your-feelings-after-birth"] =
        [
            "feel", "sad", "sadness", "cry", "crying", "tearful", "blues", "low", "mood", "depress", "anxious",
            "trauma", "birth", "emotion", "smut", "płacz", "nastr", "depresj", "lęk", "lęku", "poród", "porodzie",
            "emocj", "przygnęb"
        ],
        ["coping-with-stress"] =
        [
            "stress", "overwhelm", "anxious", "worry", "panic", "tense", "irritable", "angry", "stres", "przytłocz",
            "niepokój", "martwi", "panik", "napię", "złość", "zdenerwow"
        ],
        ["feeling-lonely"] =
        [
            "lonely", "alone", "isolated", "friend", "nobody", "support", "samotn", "sama", "izolacj", "przyjaci",
            "nikt", "wsparci"
        ],
        ["coping-with-tiredness"] =
        [
            "tired", "exhaust", "fatigue", "energy", "drained", "zmęcz", "wyczerp", "energi", "brak sił", "senn"
        ],
        ["sleeping-with-a-new-baby"] =
        [
            "sleep", "insomnia", "night", "awake", "rest", "bed", "sen", "snu", "spać", "śpię", "bezsenn", "noc",
            "nocy", "nocą", "odpoczyn", "łóżk"
        ],
        ["looking-after-yourself"] =
        [
            "self-care", "time for me", "rest", "break", "help", "dbać", "siebie", "czas dla siebie",
            "odpoczyn", "przerw", "pomoc"
        ],
        ["eating-and-drinking-well"] =
        [
            "eat", "eating", "diet", "food", "drink", "water", "caffeine", "coffee", "fish", "vitamin", "vegan",
            "breastfeeding diet", "jeść", "jedzen", "pić", "picie", "woda", "wody", "wodę", "kofein", "kawa", "kawy",
            "kawę", "ryba", "ryby", "rybę", "witamin", "wegan"
        ],
        ["baby-feeding-routine"] =
        [
            "feed", "breastfeed", "bottle", "formula", "hungry", "latch", "nappy", "diaper", "wet", "how often",
            "karmi", "pierś", "butelk", "mleko modyfik", "głod", "przystaw", "pieluch", "mokr", "jak często"
        ],
        ["crying-and-colic"] =
        [
            "cry", "crying", "cries", "colic", "scream", "fussy", "wind", "gas", "settle", "soothe", "płacz", "kolk",
            "krzycz", "marudz", "gaz", "gazy", "uspoka", "ukoić"
        ],
        ["a-day-with-your-baby"] =
        [
            "routine", "day", "schedule", "plan", "organi", "awake", "tired signs", "rutyn", "dzień", "plan",
            "harmonogram", "organiz", "czuwani", "zmęczeni"
        ],
        ["baby-nutrition-0-to-3-months"] =
        [
            "newborn", "milk", "formula", "bottle", "sterilis", "vitamin d", "noworod", "mleko", "modyfik", "butelk",
            "steryliz", "witamin"
        ],
        ["baby-nutrition-6-to-9-months"] =
        [
            "solid", "weaning", "finger food", "puree", "lumpy", "allergy", "honey", "nuts", "choke", "cup", "water",
            "stałe", "rozszerzan", "bobas", "papk", "alergi", "miód", "miodu", "orzech", "zakrztus", "kubek", "woda",
            "wody", "wodę"
        ],
        ["baby-nutrition-9-to-12-months"] =
        [
            "meal", "finger food", "chop", "cow's milk", "cup", "snack", "posiłk", "kawałk", "mleko krowie", "kubek",
            "przekąsk"
        ]
    };

    // Used only when no page matches.
    public static readonly IReadOnlyDictionary<Topic, string[]> Topics = new Dictionary<Topic, string[]>
    {
        [Topic.PostpartumWellbeing] = ["wellbeing", "tired", "sleep", "self-care", "samopoczuc", "zmęcz", "sen", "dbać"],
        [Topic.BabySleep] =
        [
            "sleep", "nap", "naps", "night", "cry", "crying", "settle", "sen", "snu", "spać", "śpi", "drzemk", "noc",
            "nocy", "płacz", "usypia"
        ],
        [Topic.BabyFeeding] = ["feed", "breastfeed", "bottle", "formula", "latch", "karmi", "pierś", "butelk", "przystaw"],
        [Topic.Nutrition] = ["diet", "food", "eat", "weaning", "solid", "dieta", "jedzen", "jeść", "rozszerzan"],
        [Topic.RecoveryAfterBirth] =
        [
            "recovery", "stitch", "caesarean", "wound", "pelvic", "połóg", "szw", "cesarsk", "rana", "miednic"
        ],
        [Topic.EmotionalSupport] =
        [
            "sad", "sadness", "anxious", "stress", "lonely", "depress", "cry", "crying", "smut", "lęk", "lęku",
            "stres", "samotn", "depresj", "płacz"
        ]
    };

    // Any of these in the search shows the urgent card first. Words in order, other words may come between.
    public static readonly string[] Urgent =
    [
        "heavy bleeding", "bleeding heavily", "chest pain", "can't breathe", "cant breathe", "not breathing",
        "struggling to breathe", "blue lips", "unconscious", "unresponsive", "seizure", "limp", "floppy", "choking",
        "suicide", "suicidal", "kill myself", "end my life", "don't want to live", "dont want to live", "harm myself",
        "hurt myself", "harm my baby", "hurt my baby",
        "silne krwawienie", "krwotok", "ból w klatce", "nie mogę oddychać", "nie moge oddychac", "nie oddycha",
        "duszność", "sine usta", "nieprzytomn", "drgawki", "wiotk", "zakrztusi", "dławi", "samobój", "zabić się",
        "zabic sie", "nie chcę żyć", "nie chce zyc", "skrzywdzić siebie", "zrobić sobie krzywdę", "skrzywdzić dziecko",
        "zrobić krzywdę dziecku"
    ];
}
