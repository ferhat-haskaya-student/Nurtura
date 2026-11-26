using System.Text.RegularExpressions;
using Nurtura.Core;
using Nurtura.Core.Demo;
using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Tests.Rules;

// The demo data keeps every rule of the design file (design doc §4 "Demo data"). The generator is checked in memory:
// the seeder itself never runs in the tests and refuses the test database. Each check runs over several moments:
// now, early morning, late evening, a Sunday, a window across the spring clock change and one across the autumn one,
// and two other seeds.
public sealed class DemoDataTests
{
    public static TheoryData<string, int> Moments()
    {
        var data = new TheoryData<string, int>
        {
            { DateTime.UtcNow.ToString("O"), DemoDataGenerator.DefaultSeed },
            { "2026-11-26T04:30:00Z", DemoDataGenerator.DefaultSeed }, // 05:30 in Poland
            { "2026-11-26T22:40:00Z", DemoDataGenerator.DefaultSeed }, // 23:40 in Poland
            { "2026-11-29T09:00:00Z", DemoDataGenerator.DefaultSeed }, // a Sunday
            { "2027-04-15T10:00:00Z", DemoDataGenerator.DefaultSeed }, // across 28 Mar 2027 (clocks forward)
            { "2026-11-10T11:00:00Z", DemoDataGenerator.DefaultSeed }, // across 25 Oct 2026 (clocks back)
            { "2026-11-26T11:00:00Z", 7 },
            { "2026-11-26T11:00:00Z", 99 }
        };
        return data;
    }

    private static DemoData Make(string nowUtc, int seed) =>
        DemoDataGenerator.Generate(DateTime.Parse(nowUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal), seed);

    private static DemoMother Mother(DemoData data, string email) => data.Mothers.Single(m => m.Email == email);

    private static bool WholeMinute(DateTime utc) => utc.Ticks % TimeSpan.TicksPerMinute == 0;

    private static DateTime SlotStartUtc(AvailabilitySlot slot)
    {
        Assert.True(PolandTime.TryToUtc(slot.Date, new TimeOnly(slot.StartHour, 0), out var utc));
        return utc;
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Sleeps_NeverOverlap_AreWholeMinutes_AndEndedBeforeNow(string now, int seed)
    {
        var data = Make(now, seed);
        foreach (var mother in data.Mothers)
        {
            var sleeps = mother.Sleeps.OrderBy(s => s.StartTime).ToList();
            Assert.NotEmpty(sleeps);
            for (var i = 0; i < sleeps.Count; i++)
            {
                var sleep = sleeps[i];
                Assert.NotNull(sleep.EndTime);
                Assert.True(sleep.EndTime > sleep.StartTime);
                Assert.True(sleep.EndTime - sleep.StartTime <= SleepRecord.MaxDuration);
                Assert.True(sleep.EndTime <= data.NowUtc);
                Assert.True(WholeMinute(sleep.StartTime) && WholeMinute(sleep.EndTime!.Value));
                Assert.True(sleep.Note is null || sleep.Note.Length <= SleepRecord.NoteMaxLength);
                Assert.Null(sleep.TimerStartedAt);
                if (i > 0)
                {
                    Assert.True(sleeps[i - 1].EndTime <= sleep.StartTime,
                        $"{mother.Email}: sleeps overlap at {sleep.StartTime:O}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Feeds_FitTheirType_AndStayInRange(string now, int seed)
    {
        var data = Make(now, seed);
        foreach (var feed in data.Mothers.SelectMany(m => m.Feeds))
        {
            switch (feed.Type)
            {
                case FeedingType.Breast:
                    Assert.NotNull(feed.BreastSide);
                    Assert.Null(feed.AmountMl);
                    Assert.Null(feed.BottleContent);
                    Assert.True(feed.Minutes is null or >= FeedingRecord.MinutesMin and <= FeedingRecord.MinutesMax);
                    break;
                case FeedingType.Bottle:
                    Assert.NotNull(feed.BottleContent);
                    Assert.Null(feed.BreastSide);
                    Assert.Null(feed.Minutes);
                    Assert.True(feed.AmountMl is null or >= FeedingRecord.AmountMlMin and <= FeedingRecord.AmountMlMax);
                    break;
                default:
                    Assert.Equal(FeedingType.Solid, feed.Type);
                    Assert.Null(feed.BreastSide);
                    Assert.Null(feed.Minutes);
                    Assert.Null(feed.AmountMl);
                    Assert.Null(feed.BottleContent);
                    Assert.False(string.IsNullOrWhiteSpace(feed.Note));
                    break;
            }

            Assert.True(WholeMinute(feed.Time));
            Assert.True(feed.Time <= data.NowUtc);
            Assert.True(feed.CreatedAt >= feed.Time && feed.CreatedAt <= data.NowUtc);
            Assert.True(feed.Note is null || feed.Note.Length <= FeedingRecord.NoteMaxLength);
        }

        // Each mother's way of feeding.
        Assert.All(Mother(data, DemoAccounts.Mother1Email).Feeds, f => Assert.Equal(FeedingType.Bottle, f.Type));
        Assert.DoesNotContain(Mother(data, DemoAccounts.Mother2Email).Feeds, f => f.Type == FeedingType.Bottle);
        Assert.Contains(Mother(data, DemoAccounts.Mother2Email).Feeds, f => f.Type == FeedingType.Solid);
        var mother3Feeds = Mother(data, DemoAccounts.Mother3Email).Feeds;
        Assert.Contains(mother3Feeds, f => f.Type == FeedingType.Breast);
        Assert.All(mother3Feeds.Where(f => f.Type == FeedingType.Bottle), f => Assert.Equal(BottleContent.Formula, f.BottleContent));
    }

    // The agreed curve (F, with research): day 1 about 100 ml, end of week 1 about 350, end of month 1 about 550,
    // month 2 about 700, month 3 about 800 a day.
    private static double AgreedMl(int day)
    {
        (int Day, double Ml)[] points = [(0, 100), (6, 350), (29, 550), (59, 700), (89, 800)];
        if (day >= points[^1].Day)
        {
            return points[^1].Ml;
        }

        var i = Array.FindIndex(points, p => p.Day > day);
        var (d0, m0) = points[i - 1];
        var (d1, m1) = points[i];
        return m0 + (m1 - m0) * (day - d0) / (d1 - d0);
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Mother1_DailyBottleMl_StaysCloseToTheAgreedCurve(string now, int seed)
    {
        var data = Make(now, seed);
        var mother1 = Mother(data, DemoAccounts.Mother1Email);
        var current = BabyRoutine.RoutineDate(data.NowUtc);

        // Full routine days (07:00 to 07:00) only; today is not finished yet.
        var days = mother1.Feeds
            .GroupBy(f => BabyRoutine.RoutineDate(f.Time))
            .Where(g => g.Key < current)
            .Select(g => (Day: g.Key.DayNumber - mother1.Baby.BirthDate.DayNumber, Ml: g.Sum(f => f.AmountMl!.Value)))
            .OrderBy(d => d.Day)
            .ToList();
        Assert.True(days.Count >= 80, $"Only {days.Count} full days with feeds.");

        // Every day within 25% (a day can miss one logged feed), every week's average within 12%.
        Assert.All(days, d => Assert.InRange(d.Ml / AgreedMl(d.Day), 0.75, 1.25));
        foreach (var week in days.Chunk(7).Where(w => w.Length == 7))
        {
            Assert.InRange(week.Average(d => d.Ml) / week.Average(d => AgreedMl(d.Day)), 0.88, 1.12);
        }

        // About 80% breast milk and 20% formula.
        var formula = mother1.Feeds.Where(f => f.BottleContent == BottleContent.Formula).Sum(f => f.AmountMl!.Value);
        Assert.InRange((double)formula / mother1.Feeds.Sum(f => f.AmountMl!.Value), 0.15, 0.25);
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Mother1_Month3_FollowsFsRoutine(string now, int seed)
    {
        var data = Make(now, seed);
        var mother1 = Mother(data, DemoAccounts.Mother1Email);
        var current = BabyRoutine.RoutineDate(data.NowUtc);
        var month3 = mother1.Baby.BirthDate.AddDays(60);

        // Night 19:00 to 07:00: the first night sleep starts from 19:00, before 19:30.
        var bedtimes = mother1.Sleeps.GroupBy(s => BabyRoutine.RoutineDate(s.StartTime))
            .Where(g => g.Key >= month3 && g.Key < current)
            .Select(g => g.Where(s => !BabyRoutine.IsNap(s.StartTime)).Min(s => (DateTime?)s.StartTime))
            .OfType<DateTime>()
            .Select(start => TimeOnly.FromDateTime(PolandTime.ToLocal(start)))
            .ToList();
        Assert.NotEmpty(bedtimes);
        Assert.All(bedtimes, t => Assert.InRange(t, new TimeOnly(19, 0), new TimeOnly(19, 30)));

        // Usually one night feed (22:00 to 06:00), more only on a bad night.
        var nightFeeds = mother1.Feeds
            .Where(f => BabyRoutine.RoutineDate(f.Time) >= month3 && BabyRoutine.RoutineDate(f.Time) < current)
            .GroupBy(f => BabyRoutine.RoutineDate(f.Time))
            .Select(g => g.Count(f => BabyRoutine.IsNightFeed(f.Time)))
            .ToList();
        Assert.NotEmpty(nightFeeds);
        Assert.True(nightFeeds.Count(n => n == 1) >= nightFeeds.Count * 0.7);
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void LoggingRates_Mother1AlmostEveryDay_Mother3AboutHalf(string now, int seed)
    {
        var data = Make(now, seed);
        var last = BabyRoutine.RoutineDate(data.NowUtc);

        double Rate(DemoMother mother, DateOnly from)
        {
            var logged = mother.Feeds.Select(f => BabyRoutine.RoutineDate(f.Time))
                .Concat(mother.Sleeps.Select(s => BabyRoutine.RoutineDate(s.StartTime)))
                .Where(d => d >= from && d < last)
                .Distinct()
                .Count();
            return (double)logged / (last.DayNumber - from.DayNumber);
        }

        Assert.True(Rate(Mother(data, DemoAccounts.Mother1Email), data.FirstDay) >= 0.85);
        Assert.InRange(Rate(Mother(data, DemoAccounts.Mother2Email), data.FirstDay), 0.7, 0.95);
        Assert.InRange(Rate(Mother(data, DemoAccounts.Mother3Email), data.FirstDay), 0.35, 0.65);
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void CheckIns_OnePerDay_RatingsOneToFive_TagsValid(string now, int seed)
    {
        var data = Make(now, seed);
        foreach (var mother in data.Mothers)
        {
            Assert.Equal(mother.CheckIns.Count, mother.CheckIns.Select(c => c.Date).Distinct().Count());
            foreach (var c in mother.CheckIns)
            {
                foreach (var rating in new[] { c.Mood, c.SleepQuality, c.Energy, c.Stress })
                {
                    Assert.InRange(rating, DailyCheckIn.RatingMin, DailyCheckIn.RatingMax);
                }

                Assert.InRange(c.Date, data.FirstDay, data.Today);
                Assert.True(c.CreatedAt <= data.NowUtc);
                Assert.Equal(c.Date, DateOnly.FromDateTime(PolandTime.ToLocal(c.CreatedAt)));
                Assert.True(c.Note is null || c.Note.Length <= DailyCheckIn.NoteMaxLength);
                var tags = c.FeelingTags.Select(t => t.FeelingTagId).ToList();
                Assert.Equal(tags.Count, tags.Distinct().Count());
                Assert.All(tags, id => Assert.InRange(id, 1, 8));
            }
        }

        // Mother 1 most days, mother 3 about half.
        var days = data.Today.DayNumber - data.FirstDay.DayNumber + 1;
        Assert.True(Mother(data, DemoAccounts.Mother1Email).CheckIns.Count >= days * 0.8);
        Assert.InRange(Mother(data, DemoAccounts.Mother3Email).CheckIns.Count, days * 0.3, days * 0.7);
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Mothers_HaveACompleteOnboarding(string now, int seed)
    {
        var data = Make(now, seed);
        Assert.Equal(DemoAccounts.MotherEmails.Order(), data.Mothers.Select(m => m.Email).Order());
        foreach (var mother in data.Mothers)
        {
            Assert.NotEmpty(mother.Concerns);
            Assert.Equal(mother.Concerns.Count, mother.Concerns.Distinct().Count());
            Assert.NotEmpty(mother.SupportTypes);
            Assert.Equal(mother.SupportTypes.Count, mother.SupportTypes.Distinct().Count());

            var profile = mother.Profile;
            Assert.False(string.IsNullOrWhiteSpace(profile.DisplayName));
            Assert.NotNull(profile.IsFirstBaby);
            Assert.NotNull(profile.HelpAtHome);
            Assert.NotNull(profile.OnboardingCompletedAt);
            Assert.True(profile.OnboardingCompletedAt >= profile.CreatedAt);

            // Onboarded before her first log, with a baby born by then.
            var firstLog = mother.Sleeps.Select(s => s.StartTime)
                .Concat(mother.Feeds.Select(f => f.Time))
                .Concat(mother.CheckIns.Select(c => c.CreatedAt))
                .Min();
            Assert.True(profile.OnboardingCompletedAt <= firstLog);

            var baby = mother.Baby;
            Assert.False(string.IsNullOrWhiteSpace(baby.Name));
            Assert.True(baby.BirthDate <= DateOnly.FromDateTime(PolandTime.ToLocal(profile.OnboardingCompletedAt!.Value)));
            Assert.InRange(baby.BirthWeek, Baby.BirthWeekMin, Baby.BirthWeekMax);
        }
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Slots_MondayToSaturday_08To18_OpenForTheNext4Weeks(string now, int seed)
    {
        var data = Make(now, seed);
        foreach (var professional in data.Professionals)
        {
            Assert.All(professional.Slots, s =>
            {
                Assert.False(AvailabilitySlot.IsDayOff(s.Date));
                Assert.InRange(s.StartHour, AvailabilitySlot.FirstHour, AvailabilitySlot.LastHour);
            });
            Assert.Equal(professional.Slots.Count,
                professional.Slots.Select(s => (s.Date, s.StartHour)).Distinct().Count());

            if (professional.Profile.ApprovalStatus != ApprovalStatus.Approved)
            {
                Assert.Empty(professional.Slots);
                continue;
            }

            // Open hours in each of the next 4 weeks, never beyond them.
            for (var week = 0; week < 4; week++)
            {
                var from = data.Today.AddDays(7 * week);
                Assert.Contains(professional.Slots, s => s.IsOpen && s.Date >= from && s.Date < from.AddDays(7));
            }

            Assert.DoesNotContain(professional.Slots, s => s.Date >= data.Today.AddDays(Appointment.MaxDaysAhead));
        }

        Assert.Equal(4, data.Professionals.Count(p => p.Profile.ApprovalStatus == ApprovalStatus.Approved));
        Assert.Single(data.Professionals, p => p.Profile.ApprovalStatus == ApprovalStatus.Pending);
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Appointments_StatusesFitTheDates(string now, int seed)
    {
        var data = Make(now, seed);
        var nowUtc = data.NowUtc;

        foreach (var a in data.Appointments)
        {
            var appointment = a.Appointment;
            var start = SlotStartUtc(a.Slot);
            var end = appointment.SessionEndedAt is { } ended && ended < start + Appointment.Length
                ? ended
                : start + Appointment.Length;

            Assert.Contains(a.Slot, a.Professional.Slots);
            Assert.Contains(appointment.Topic, a.Professional.Topics);
            Assert.Equal(ApprovalStatus.Approved, a.Professional.Profile.ApprovalStatus);
            Assert.True(appointment.ConcernText.Length is > 0 and <= Appointment.ConcernMaxLength);

            // Requested from 12 hours to 4 weeks ahead.
            Assert.True(appointment.CreatedAt <= start - Appointment.MinNotice);
            Assert.True(appointment.CreatedAt >= start.AddDays(-Appointment.MaxDaysAhead));
            Assert.True(appointment.CreatedAt <= appointment.UpdatedAt && appointment.UpdatedAt <= nowUtc);

            switch (appointment.Status)
            {
                case AppointmentStatus.Requested:
                    Assert.True(start - Appointment.ConfirmBefore > nowUtc);
                    break;
                case AppointmentStatus.Confirmed:
                    Assert.True(start > nowUtc);
                    break;
                case AppointmentStatus.Completed:
                    Assert.NotNull(a.Note);
                    Assert.True(a.Note!.CreatedAt >= end && a.Note.CreatedAt <= nowUtc);
                    Assert.Equal(a.Note.CreatedAt, a.Note.UpdatedAt);
                    Assert.True(a.Note.Text.Length <= ConsultationNote.TextMaxLength);
                    Assert.NotEmpty(a.Messages);
                    break;
                case AppointmentStatus.Cancelled:
                    Assert.NotNull(appointment.CancelledBy);
                    Assert.True(appointment.UpdatedAt < start - Appointment.RoomOpensBefore);
                    break;
                case AppointmentStatus.Rejected:
                    Assert.False(string.IsNullOrWhiteSpace(appointment.ProfessionalMessage));
                    Assert.True(appointment.ProfessionalMessage!.Length <= Appointment.MessageMaxLength);
                    break;
                case AppointmentStatus.Expired:
                    Assert.True(start - Appointment.ConfirmBefore <= nowUtc);
                    break;
            }

            if (appointment.Status != AppointmentStatus.Completed)
            {
                Assert.Null(a.Note);
                Assert.Empty(a.Messages);
            }

            Assert.Equal(appointment.SessionEndedAt is null, appointment.SessionEndedBy is null);

            // Chat only inside the session, in order.
            var previous = DateTime.MinValue;
            foreach (var message in a.Messages)
            {
                Assert.InRange(message.SentAt, start - Appointment.RoomOpensBefore, end);
                Assert.True(message.SentAt > previous);
                Assert.True(message.Text.Length is > 0 and <= ChatMessage.TextMaxLength);
                previous = message.SentAt;
            }
        }

        // One active appointment per slot; at most 3 upcoming per mother, never two at the same hour.
        var active = data.Appointments.Where(a => Appointment.ActiveStatuses.Contains(a.Appointment.Status)).ToList();
        Assert.Equal(active.Count, active.Select(a => a.Slot).Distinct().Count());
        foreach (var mother in data.Mothers)
        {
            var upcoming = active.Where(a => a.Mother == mother).ToList();
            Assert.True(upcoming.Count <= Appointment.MaxUpcoming);
            Assert.Equal(upcoming.Count, upcoming.Select(a => (a.Slot.Date, a.Slot.StartHour)).Distinct().Count());
        }

        // Mother 1 has a confirmed one in the next days, mother 3 a request; every past status appears.
        Assert.Contains(active, a => a.Mother.Email == DemoAccounts.Mother1Email
                                     && a.Appointment.Status == AppointmentStatus.Confirmed
                                     && SlotStartUtc(a.Slot) < nowUtc.AddDays(7));
        Assert.Contains(active, a => a.Mother.Email == DemoAccounts.Mother3Email
                                     && a.Appointment.Status == AppointmentStatus.Requested);
        foreach (var status in new[]
                 {
                     AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.Rejected,
                     AppointmentStatus.Expired
                 })
        {
            Assert.Contains(data.Appointments, a => a.Appointment.Status == status);
        }
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Mother1_SharesHerLast2Weeks_ForTheUpcomingConsultation(string now, int seed)
    {
        var data = Make(now, seed);
        var mother1 = Mother(data, DemoAccounts.Mother1Email);
        var from = data.Today.AddDays(-(Appointment.ShareDays - 1));
        var since = data.NowUtc.AddDays(-Appointment.ShareDays);

        Assert.All(mother1.CheckIns.Where(c => c.Date >= from), c => Assert.True(c.IsShared));
        Assert.All(mother1.Sleeps.Where(s => s.StartTime >= since), s => Assert.True(s.IsShared));
        Assert.All(mother1.Feeds.Where(f => f.Time >= since), f => Assert.True(f.IsShared));

        // Older days stay private unless an earlier booking shared them.
        Assert.Contains(mother1.CheckIns, c => !c.IsShared);
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Notifications_FitWhatHappened(string now, int seed)
    {
        var data = Make(now, seed);
        Assert.NotEmpty(data.Notifications);
        foreach (var item in data.Notifications)
        {
            var n = item.Notification;
            Assert.True(n.CreatedAt <= data.NowUtc);
            Assert.True(n.CreatedAt >= data.NowUtc.AddDays(-Notification.KeepReadDays));
            Assert.Equal(n.CreatedAt < data.NowUtc.AddDays(-2), n.IsRead);

            if (n.Type == NotificationType.CommentOnYourPost)
            {
                Assert.Equal(item.Post!.Author.Email, item.RecipientEmail);
                Assert.NotEqual(item.Post.Author, item.Comment!.Author);
                Assert.Equal(item.Comment.Author.Profile.DisplayName, n.OtherName);
                continue;
            }

            var a = item.Appointment!;
            var toProfessional = n.Type is NotificationType.NewRequest or NotificationType.CancelledByMother;
            Assert.Equal(toProfessional ? a.Professional.Email : a.Mother.Email, item.RecipientEmail);
            Assert.Equal(toProfessional ? a.Mother.Profile.DisplayName : a.Professional.Profile.DisplayName, n.OtherName);
            Assert.Equal(a.Slot.Date, n.Date);
            Assert.Equal(a.Slot.StartHour, n.Hour);
            Assert.Equal(n.Type == NotificationType.RequestRejected, n.Message is not null);
        }
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void Forum_PostsCommentsReportAndSavedPost(string now, int seed)
    {
        var data = Make(now, seed);
        Assert.Equal(8, data.Posts.Count);
        Assert.Equal(20, data.Posts.Sum(p => p.Comments.Count));
        Assert.Equal(8, data.Posts.Select(p => p.Post.ForumCategoryId).Distinct().Count());

        foreach (var post in data.Posts)
        {
            Assert.Equal(Language.English, post.Post.Language);
            Assert.True(post.Post.Title.Length is > 0 and <= ForumPost.TitleMaxLength);
            Assert.True(post.Post.Body.Length is > 0 and <= ForumPost.BodyMaxLength);
            Assert.DoesNotContain("\r", post.Post.Body);
            Assert.InRange(post.Post.CreatedAt, DemoTimeOf(data.FirstDay), data.NowUtc);
            foreach (var comment in post.Comments)
            {
                Assert.True(comment.Comment.Body.Length is > 0 and <= ForumComment.BodyMaxLength);
                Assert.True(comment.Comment.CreatedAt > post.Post.CreatedAt);
                Assert.True(comment.Comment.CreatedAt <= data.NowUtc);
            }
        }

        var report = Assert.Single(data.CommentReports);
        Assert.Equal(ReportReason.Unkind, report.Report.Reason);
        Assert.Equal(ReportStatus.Open, report.Report.Status);
        Assert.NotEqual(report.Reporter, report.Comment.Author);
        Assert.True(report.Report.CreatedAt > report.Comment.Comment.CreatedAt && report.Report.CreatedAt <= data.NowUtc);

        var saved = Assert.Single(data.SavedPosts);
        Assert.NotEqual(saved.Mother, saved.Post.Author);
        Assert.True(saved.Saved.SavedAt > saved.Post.Post.CreatedAt && saved.Saved.SavedAt <= data.NowUtc);
    }

    private static DateTime DemoTimeOf(DateOnly date)
    {
        PolandTime.TryToUtc(date, TimeOnly.MinValue, out var utc);
        return utc;
    }

    [Theory]
    [MemberData(nameof(Moments))]
    public void AssistantSearches_SpreadOverThe3Months(string now, int seed)
    {
        var data = Make(now, seed);
        Assert.Equal(40, data.AssistantQueries.Count);
        Assert.All(data.AssistantQueries, q =>
        {
            Assert.InRange(q.Date, data.FirstDay, data.Today);
            if (!q.Matched)
            {
                Assert.Null(q.SuggestedTopic);
            }
        });
        Assert.True(data.AssistantQueries.Count(q => q.UrgentShown) >= 2);
        Assert.Contains(data.AssistantQueries, q => !q.Matched);
        Assert.Contains(data.AssistantQueries, q => q.Matched && q.SuggestedTopic is null);
    }

    // No diagnosis, no medicine names or doses, no medical words anywhere in what the demo people wrote.
    private static readonly Regex MedicalWords = new(
        @"\b(dose|doses|dosage|mg|medicine|medicines|medication|medications|pill|pills|tablet|tablets|paracetamol|" +
        @"ibuprofen|prescri\w*|diagnos\w*|disorder|depression|infection|reflux|colic)\b",
        RegexOptions.IgnoreCase);

    [Fact]
    public void Texts_HaveNoDiagnosisOrMedicine()
    {
        var data = DemoDataGenerator.Generate(DateTime.UtcNow);
        var written = WrittenTexts(data);
        Assert.True(written.Count > 50);
        Assert.All(written, text => Assert.False(MedicalWords.IsMatch(text), text));
        Assert.All(data.Professionals, p => Assert.True(p.Profile.Bio.Length <= ProfessionalProfile.BioMaxLength));
    }

    // The first run's first names and surnames (F, 26 Nov 2026: names by type instead).
    private static readonly Regex OldNames = new(
        @"\b(Kasia|Marta|Ola|Zosia|Antek|Lena|Anna|Ewa|Magda|Joanna|Natalia|Nowak|Kowalska|Wiśniewska|Zielińska|" +
        @"Lewandowska)\b",
        RegexOptions.IgnoreCase);

    [Fact]
    public void Names_AreByType_AndNoTextHasAPersonalName()
    {
        var data = DemoDataGenerator.Generate(DateTime.UtcNow);

        Assert.Equal(["Mother 1", "Mother 2", "Mother 3"], data.Mothers.Select(m => m.Profile.DisplayName));
        Assert.Equal(["Baby 1", "Baby 2", "Baby 3"], data.Mothers.Select(m => m.Baby.Name));
        Assert.Equal(
            ["mother1@demo.nurtura", "mother2@demo.nurtura", "mother3@demo.nurtura"],
            data.Mothers.Select(m => m.Email));
        Assert.Equal(
            [
                ("midwife@demo.nurtura", "Midwife", ApprovalStatus.Approved),
                ("lactation@demo.nurtura", "Lactation consultant", ApprovalStatus.Approved),
                ("sleep@demo.nurtura", "Sleep consultant", ApprovalStatus.Approved),
                ("psychologist@demo.nurtura", "Psychologist", ApprovalStatus.Approved),
                ("dietitian@demo.nurtura", "Dietitian", ApprovalStatus.Pending)
            ],
            data.Professionals.Select(p => (p.Email, p.Profile.DisplayName, p.Profile.ApprovalStatus)));

        // Not in any text, name, email or notification either.
        var everything = WrittenTexts(data)
            .Concat(data.Emails)
            .Concat(data.Mothers.Select(m => m.Profile.DisplayName))
            .Concat(data.Mothers.Select(m => m.Baby.Name))
            .Concat(data.Professionals.Select(p => p.Profile.DisplayName))
            .Concat(data.Notifications.Select(n => n.Notification.OtherName));
        Assert.All(everything, text => Assert.False(OldNames.IsMatch(text), text));
    }

    // Everything the demo people wrote.
    private static List<string> WrittenTexts(DemoData data)
    {
        var texts = new List<string?>();
        foreach (var mother in data.Mothers)
        {
            texts.AddRange(mother.Sleeps.Select(s => s.Note));
            texts.AddRange(mother.Feeds.Select(f => f.Note));
            texts.AddRange(mother.CheckIns.Select(c => c.Note));
        }

        texts.AddRange(data.Professionals.Select(p => p.Profile.Bio));
        foreach (var a in data.Appointments)
        {
            texts.Add(a.Appointment.ConcernText);
            texts.Add(a.Appointment.ProfessionalMessage);
            texts.Add(a.Note?.Text);
            texts.AddRange(a.Messages.Select(m => m.Text));
        }

        foreach (var post in data.Posts)
        {
            texts.Add(post.Post.Title);
            texts.Add(post.Post.Body);
            texts.AddRange(post.Comments.Select(c => c.Comment.Body));
        }

        texts.AddRange(data.CommentReports.Select(r => r.Report.Note));
        return texts.OfType<string>().Distinct().ToList();
    }

    [Fact]
    public void Seeder_OnlyInDevelopment_NeverOnTheTestDatabase()
    {
        Assert.NotNull(DemoSeeder.RefusalReason(isDevelopment: false, "nurtura"));
        Assert.NotNull(DemoSeeder.RefusalReason(isDevelopment: true, "nurtura_test"));
        Assert.NotNull(DemoSeeder.RefusalReason(isDevelopment: false, "nurtura_test"));
        Assert.Null(DemoSeeder.RefusalReason(isDevelopment: true, "nurtura"));
        Assert.Equal("--seed-demo", DemoSeeder.Flag);
        Assert.Equal("--reset-demo", DemoSeeder.ResetFlag);
    }

    [Fact]
    public void Generate_SameMomentAndSeed_GivesTheSameData()
    {
        var now = new DateTime(2026, 11, 26, 11, 0, 0, DateTimeKind.Utc);
        var a = DemoDataGenerator.Generate(now);
        var b = DemoDataGenerator.Generate(now);
        Assert.Equal(
            a.Mothers.SelectMany(m => m.Feeds).Select(f => (f.Time, f.AmountMl)),
            b.Mothers.SelectMany(m => m.Feeds).Select(f => (f.Time, f.AmountMl)));
        Assert.Equal(
            a.Mothers.SelectMany(m => m.CheckIns).Select(c => (c.Date, c.Mood, c.Stress)),
            b.Mothers.SelectMany(m => m.CheckIns).Select(c => (c.Date, c.Mood, c.Stress)));
    }
}
