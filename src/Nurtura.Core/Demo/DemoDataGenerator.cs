using Nurtura.Core.Entities;
using Nurtura.Core.Services;

namespace Nurtura.Core.Demo;

// Builds the demo data in memory, without the database (design doc §4 "Demo data"). Dates are worked out from
// `utcNow`, so the data always ends then. The same seed and the same moment always give the same data.
public static class DemoDataGenerator
{
    public const int DefaultSeed = 2026;

    // Notifications older than this would have been read and deleted by now (Notification.KeepReadDays).
    private static readonly TimeSpan KeptFor = TimeSpan.FromDays(Notification.KeepReadDays);

    // Notifications from the last 2 days are still unread.
    private static readonly TimeSpan UnreadFor = TimeSpan.FromDays(2);

    public static DemoData Generate(DateTime utcNow, int seed = DefaultSeed)
    {
        var nowUtc = SleepService.ToMinute(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
        var today = DemoTime.LocalDate(nowUtc);
        var first = today.AddMonths(-3);
        var data = new DemoData { NowUtc = nowUtc, Today = today, FirstDay = first };

        AddMothers(data, seed);
        AddProfessionals(data, new Random(seed + 10));
        AddAppointments(data, new Random(seed + 11));
        AddForum(data, new Random(seed + 12));
        AddAssistantQueries(data, new Random(seed + 13));
        return data;
    }

    private static void AddMothers(DemoData data, int seed)
    {
        var (first, today, nowUtc) = (data.FirstDay, data.Today, data.NowUtc);
        var last = BabyRoutine.RoutineDate(nowUtc);

        var kasia = Mother(data, DemoAccounts.KasiaEmail, "Kasia", isFirstBaby: true, HelpAtHome.Yes,
            [Topic.BabySleep, Topic.BabyFeeding, Topic.PostpartumWellbeing],
            [SupportType.TalkingToProfessional, SupportType.ReadingGuides],
            new Baby
            {
                Name = "Zosia", BirthDate = first, BirthType = BirthType.Natural, BirthWeek = 39,
                FeedingType = BabyFeedingType.Bottle
            });
        var marta = Mother(data, DemoAccounts.MartaEmail, "Marta", isFirstBaby: false, HelpAtHome.Sometimes,
            [Topic.BabyFeeding, Topic.Nutrition],
            [SupportType.ReadingGuides, SupportType.JustTracking],
            new Baby
            {
                Name = "Antek", BirthDate = today.AddMonths(-7), BirthType = BirthType.CSection, BirthWeek = 40,
                FeedingType = BabyFeedingType.StartingSolids
            });
        var ola = Mother(data, DemoAccounts.OlaEmail, "Ola", isFirstBaby: true, HelpAtHome.MostlyAlone,
            [Topic.EmotionalSupport, Topic.BabyFeeding],
            [SupportType.SharingWithMothers, SupportType.TalkingToProfessional],
            new Baby
            {
                Name = "Lena", BirthDate = today.AddMonths(-4), BirthType = BirthType.Natural, BirthWeek = 38,
                FeedingType = BabyFeedingType.Both
            });

        // A few bad nights each; mother 1 has more of them in the first 6 weeks.
        var rng = new Random(seed);
        var kasiaBad = rng.Sample(Dates(first.AddDays(3), first.AddDays(41)), 4);
        kasiaBad.UnionWith(rng.Sample(Dates(first.AddDays(42), last.AddDays(-1)), 3));
        var martaBad = rng.Sample(Dates(first, last.AddDays(-1)), 5);
        var olaBad = rng.Sample(Dates(first, last.AddDays(-1)), 5);

        DemoBabyLog.Kasia(kasia, last, nowUtc, kasiaBad, new Random(seed + 1));
        DemoBabyLog.Marta(marta, first, last, nowUtc, martaBad, new Random(seed + 2));
        DemoBabyLog.Ola(ola, first, last, nowUtc, olaBad, new Random(seed + 3));

        DemoCheckIns.Generate(kasia,
            new CheckInStyle(0.9, 0, 0, 0, 0, 0.25, false, 0.25, [], AlwaysToday: true),
            first, nowUtc, kasiaBad, new Random(seed + 4));
        DemoCheckIns.Generate(marta,
            new CheckInStyle(0.8, 0.1, -0.4, -0.2, 0.3, 0.22, false, 0.1, DemoTexts.CheckInMarta, AlwaysToday: false),
            first, nowUtc, martaBad, new Random(seed + 5));
        DemoCheckIns.Generate(ola,
            new CheckInStyle(0.5, -0.4, -0.1, -0.1, 0.3, 0.35, true, 0.05, DemoTexts.CheckInOla, AlwaysToday: false),
            first, nowUtc, olaBad, new Random(seed + 6));
    }

    // She signed up and finished onboarding early on the first day, before her first log.
    private static DemoMother Mother(
        DemoData data, string email, string name, bool isFirstBaby, HelpAtHome help, List<Topic> concerns,
        List<SupportType> supportTypes, Baby baby)
    {
        var signedUp = DemoTime.Utc(data.FirstDay, 6, 0);
        var mother = new DemoMother
        {
            Email = email,
            Profile = new MotherProfile
            {
                DisplayName = name,
                IsFirstBaby = isFirstBaby,
                HelpAtHome = help,
                CreatedAt = signedUp,
                OnboardingCompletedAt = signedUp.AddMinutes(7)
            },
            Concerns = concerns,
            SupportTypes = supportTypes,
            Baby = baby
        };
        data.Mothers.Add(mother);
        return mother;
    }

    private static void AddProfessionals(DemoData data, Random rng)
    {
        foreach (var account in DemoAccounts.Professionals)
        {
            var approved = account.Status == ApprovalStatus.Approved;
            var professional = new DemoProfessional
            {
                Email = account.Email,
                Profile = new ProfessionalProfile
                {
                    DisplayName = account.DisplayName,
                    ProfessionalType = account.Type,
                    Bio = account.Bio,
                    ApprovalStatus = account.Status,
                    IsActive = true,
                    CreatedAt = approved
                        ? DemoTime.Utc(data.FirstDay.AddDays(-40), 10, 0)
                        : data.NowUtc.AddDays(-2).AddHours(-rng.Pick(1, 6))
                },
                Topics = [.. account.Topics],
                Languages = [.. account.Languages]
            };

            // Open hours from today for the next 4 weeks, Monday to Saturday.
            var opened = data.NowUtc.AddDays(-rng.Pick(7, 12));
            for (var date = data.Today; date < data.Today.AddDays(Appointment.MaxDaysAhead); date = date.AddDays(1))
            {
                if (AvailabilitySlot.IsDayOff(date) || !account.Hours.TryGetValue(date.DayOfWeek, out var hours))
                {
                    continue;
                }

                foreach (var hour in hours)
                {
                    professional.Slots.Add(new AvailabilitySlot
                    {
                        Date = date, StartHour = hour, IsOpen = true, CreatedAt = opened, UpdatedAt = opened
                    });
                }
            }

            data.Professionals.Add(professional);
        }
    }

    private sealed record PastBooking(
        string MotherEmail,
        string ProfessionalEmail,
        DateOnly Target,
        int HourIndex,
        Topic Topic,
        string Concern,
        AppointmentStatus Status,
        bool Share = false,
        (AppointmentParty From, string Text)[]? Chat = null,
        string? Note = null,
        AppointmentParty? EndedBy = null,
        int EndedAfterMinutes = 0);

    private static void AddAppointments(DemoData data, Random rng)
    {
        var (first, today, nowUtc) = (data.FirstDay, data.Today, data.NowUtc);
        PastBooking[] past =
        [
            new(DemoAccounts.KasiaEmail, DemoAccounts.EwaEmail, first.AddDays(12), 1, Topic.BabyFeeding,
                DemoTexts.ConcernKasiaFeeding, AppointmentStatus.Completed, Share: true,
                Chat: DemoTexts.ChatKasiaFeeding, Note: DemoTexts.NoteKasiaFeeding),
            new(DemoAccounts.KasiaEmail, DemoAccounts.AnnaEmail, first.AddDays(35), 1, Topic.RecoveryAfterBirth,
                DemoTexts.ConcernKasiaRecovery, AppointmentStatus.Completed,
                Chat: DemoTexts.ChatKasiaRecovery, Note: DemoTexts.NoteKasiaRecovery,
                EndedBy: AppointmentParty.Professional, EndedAfterMinutes: 52),
            new(DemoAccounts.KasiaEmail, DemoAccounts.MagdaEmail, first.AddDays(50), 1, Topic.BabySleep,
                DemoTexts.ConcernKasiaNaps, AppointmentStatus.Cancelled),
            new(DemoAccounts.KasiaEmail, DemoAccounts.JoannaEmail, first.AddDays(70), 2, Topic.EmotionalSupport,
                DemoTexts.ConcernKasiaFeelings, AppointmentStatus.Completed, Share: true,
                Chat: DemoTexts.ChatKasiaFeelings, Note: DemoTexts.NoteKasiaFeelings),
            new(DemoAccounts.MartaEmail, DemoAccounts.EwaEmail, today.AddDays(-14), 0, Topic.BabyFeeding,
                DemoTexts.ConcernMartaSolids, AppointmentStatus.Completed, Share: true,
                Chat: DemoTexts.ChatMartaSolids, Note: DemoTexts.NoteMartaSolids,
                EndedBy: AppointmentParty.Mother, EndedAfterMinutes: 47),
            new(DemoAccounts.MartaEmail, DemoAccounts.AnnaEmail, today.AddDays(-10), 2, Topic.PostpartumWellbeing,
                DemoTexts.ConcernMartaEnergy, AppointmentStatus.Expired),
            new(DemoAccounts.OlaEmail, DemoAccounts.JoannaEmail, first.AddDays(48), 0, Topic.EmotionalSupport,
                DemoTexts.ConcernOlaLonely, AppointmentStatus.Completed, Share: true,
                Chat: DemoTexts.ChatOlaLonely, Note: DemoTexts.NoteOlaLonely),
            new(DemoAccounts.OlaEmail, DemoAccounts.MagdaEmail, today.AddDays(-21), 2, Topic.BabySleep,
                DemoTexts.ConcernOlaBedtime, AppointmentStatus.Rejected)
        ];

        foreach (var booking in past)
        {
            var mother = data.Mothers.Single(m => m.Email == booking.MotherEmail);
            var professional = data.Professionals.Single(p => p.Email == booking.ProfessionalEmail);
            var slot = PastSlot(professional, booking.Target, booking.HourIndex);
            var start = Start(slot);
            var created = start.AddDays(-rng.Pick(2, 6)).AddMinutes(-rng.Pick(0, 480));
            var appointment = Book(data, mother, professional, slot, booking.Topic, booking.Concern, created);

            switch (booking.Status)
            {
                case AppointmentStatus.Completed:
                    Accept(data, appointment, created.AddMinutes(rng.Pick(60, 360)));
                    var end = start + Appointment.Length;
                    if (booking.EndedBy is { } endedBy)
                    {
                        end = start.AddMinutes(booking.EndedAfterMinutes);
                        appointment.Appointment.SessionEndedAt = end;
                        appointment.Appointment.SessionEndedBy = endedBy;
                    }

                    Chat(appointment, booking.Chat!, start, end, rng);
                    var written = end.AddMinutes(rng.Pick(60, 360));
                    if (written > nowUtc)
                    {
                        written = nowUtc;
                    }

                    appointment.Note = new ConsultationNote
                    {
                        Text = booking.Note!, CreatedAt = written, UpdatedAt = written
                    };
                    appointment.Appointment.Status = AppointmentStatus.Completed;
                    appointment.Appointment.UpdatedAt = written;
                    Notify(data, mother.Email, NotificationType.ClosingNoteWritten, appointment,
                        professional.Profile.DisplayName, written);
                    break;

                case AppointmentStatus.Cancelled:
                    Accept(data, appointment, created.AddMinutes(rng.Pick(60, 360)));
                    // She cancelled the day before, long before the room opened.
                    var cancelled = start.AddDays(-1).AddMinutes(-rng.Pick(60, 300));
                    appointment.Appointment.Status = AppointmentStatus.Cancelled;
                    appointment.Appointment.CancelledBy = AppointmentParty.Mother;
                    appointment.Appointment.UpdatedAt = cancelled;
                    Notify(data, professional.Email, NotificationType.CancelledByMother, appointment,
                        mother.Profile.DisplayName, cancelled);
                    break;

                case AppointmentStatus.Rejected:
                    var rejected = created.AddMinutes(rng.Pick(120, 480));
                    appointment.Appointment.Status = AppointmentStatus.Rejected;
                    appointment.Appointment.ProfessionalMessage = DemoTexts.RejectMessage;
                    appointment.Appointment.UpdatedAt = rejected;
                    Notify(data, mother.Email, NotificationType.RequestRejected, appointment,
                        professional.Profile.DisplayName, rejected, DemoTexts.RejectMessage);
                    break;

                case AppointmentStatus.Expired:
                    // Not confirmed by 2 hours before the start.
                    var expired = start - Appointment.ConfirmBefore;
                    appointment.Appointment.Status = AppointmentStatus.Expired;
                    appointment.Appointment.UpdatedAt = expired;
                    Notify(data, mother.Email, NotificationType.RequestExpired, appointment,
                        professional.Profile.DisplayName, expired);
                    break;
            }

            if (booking.Share)
            {
                Share(mother, appointment, created);
            }
        }

        // Mother 1: a confirmed consultation in the next days, her last 2 weeks shared.
        var kasia = data.Mothers.Single(m => m.Email == DemoAccounts.KasiaEmail);
        var magda = data.Professionals.Single(p => p.Email == DemoAccounts.MagdaEmail);
        var kasiaSlot = magda.Slots.OrderBy(Start).First(s => Start(s) >= nowUtc.AddHours(36));
        var kasiaCreated = nowUtc.AddDays(-2).AddMinutes(-rng.Pick(0, 240));
        var kasiaNext = Book(data, kasia, magda, kasiaSlot, Topic.BabySleep, DemoTexts.ConcernKasiaRoutine, kasiaCreated);
        Accept(data, kasiaNext, kasiaCreated.AddMinutes(rng.Pick(120, 300)));
        Share(kasia, kasiaNext, nowUtc);

        // Mother 3: a request waiting for the professional, well before it would expire.
        var ola = data.Mothers.Single(m => m.Email == DemoAccounts.OlaEmail);
        var anna = data.Professionals.Single(p => p.Email == DemoAccounts.AnnaEmail);
        var olaSlot = anna.Slots.OrderBy(Start).First(s => Start(s) >= nowUtc.AddDays(3));
        var olaCreated = nowUtc.AddHours(-rng.Pick(18, 30));
        var olaNext = Book(data, ola, anna, olaSlot, Topic.BabyFeeding, DemoTexts.ConcernOlaFeeding, olaCreated);
        Share(ola, olaNext, nowUtc);
    }

    private static DateTime Start(AvailabilitySlot slot) => DemoTime.Utc(slot.Date, slot.StartHour, 0);

    // The slot of a past appointment: her working day on or before the target, the given hour of her pattern.
    private static AvailabilitySlot PastSlot(DemoProfessional professional, DateOnly target, int hourIndex)
    {
        var account = DemoAccounts.Professionals.Single(a => a.Email == professional.Email);
        var date = target;
        while (AvailabilitySlot.IsDayOff(date) || !account.Hours.ContainsKey(date.DayOfWeek))
        {
            date = date.AddDays(-1);
        }

        var hours = account.Hours[date.DayOfWeek];
        var hour = hours[Math.Min(hourIndex, hours.Length - 1)];
        var opened = DemoTime.Utc(date.AddDays(-10), 9, 0);
        var slot = new AvailabilitySlot { Date = date, StartHour = hour, IsOpen = true, CreatedAt = opened, UpdatedAt = opened };
        professional.Slots.Add(slot);
        return slot;
    }

    // A request, as BookingService saves it, with the professional's NewRequest notification.
    private static DemoAppointment Book(
        DemoData data, DemoMother mother, DemoProfessional professional, AvailabilitySlot slot, Topic topic,
        string concern, DateTime created)
    {
        var appointment = new DemoAppointment
        {
            Mother = mother,
            Professional = professional,
            Slot = slot,
            Appointment = new Appointment
            {
                Topic = topic,
                ConcernText = concern,
                Status = AppointmentStatus.Requested,
                CreatedAt = created,
                UpdatedAt = created
            }
        };
        data.Appointments.Add(appointment);
        Notify(data, professional.Email, NotificationType.NewRequest, appointment, mother.Profile.DisplayName, created);
        return appointment;
    }

    private static void Accept(DemoData data, DemoAppointment appointment, DateTime accepted)
    {
        appointment.Appointment.Status = AppointmentStatus.Confirmed;
        appointment.Appointment.UpdatedAt = accepted;
        Notify(data, appointment.Mother.Email, NotificationType.RequestAccepted, appointment,
            appointment.Professional.Profile.DisplayName, accepted);
    }

    // The messages, in order, between the start and the end of the session.
    private static void Chat(
        DemoAppointment appointment, (AppointmentParty From, string Text)[] script, DateTime start, DateTime end,
        Random rng)
    {
        var gaps = script.Select(_ => rng.Pick(40, 330)).ToArray();
        var limit = (end - start).TotalSeconds - 60;
        var scale = Math.Min(1, limit / gaps.Sum());
        var sent = start;
        for (var i = 0; i < script.Length; i++)
        {
            sent = sent.AddSeconds(Math.Max(20, (int)(gaps[i] * scale)));
            appointment.Messages.Add(new ChatMessage { Sender = script[i].From, Text = script[i].Text, SentAt = sent });
        }
    }

    // One-tap sharing, as on the request page: check-ins from the 14 days up to that day, and for baby topics the
    // baby's sleeps and feeds from the 14 days before that moment. For the upcoming ones the window ends now
    // (she switched the newer records to shared too).
    private static void Share(DemoMother mother, DemoAppointment appointment, DateTime until)
    {
        var shared = appointment.Appointment.CreatedAt;
        var to = DemoTime.LocalDate(until);
        var from = to.AddDays(-(Appointment.ShareDays - 1));
        foreach (var checkIn in mother.CheckIns.Where(c => c.Date >= from && c.Date <= to))
        {
            checkIn.IsShared = true;
            checkIn.UpdatedAt = Later(checkIn.UpdatedAt, shared);
        }

        if (!appointment.AboutBaby)
        {
            return;
        }

        var since = until.AddDays(-Appointment.ShareDays);
        foreach (var sleep in mother.Sleeps.Where(s => s.StartTime >= since && s.StartTime <= until))
        {
            sleep.IsShared = true;
            sleep.UpdatedAt = Later(sleep.UpdatedAt, shared);
        }

        foreach (var feed in mother.Feeds.Where(f => f.Time >= since && f.Time <= until))
        {
            feed.IsShared = true;
            feed.UpdatedAt = Later(feed.UpdatedAt, shared);
        }
    }

    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;

    // The same notification the app writes. One older than 30 days would have been read and deleted, so it is
    // left out; the last 2 days are unread.
    private static void Notify(
        DemoData data, string email, NotificationType type, DemoAppointment appointment, string otherName,
        DateTime at, string? message = null)
    {
        if (at < data.NowUtc - KeptFor)
        {
            return;
        }

        data.Notifications.Add(new DemoNotification
        {
            RecipientEmail = email,
            Appointment = appointment,
            Notification = new Notification
            {
                Type = type,
                OtherName = otherName,
                Date = appointment.Slot.Date,
                Hour = appointment.Slot.StartHour,
                Message = message,
                CreatedAt = at,
                IsRead = at < data.NowUtc - UnreadFor
            }
        });
    }

    private static void AddForum(DemoData data, Random rng)
    {
        DemoMother By(string email) => data.Mothers.Single(m => m.Email == email);

        foreach (var text in DemoTexts.Posts)
        {
            var date = text.Day >= 0 ? data.FirstDay.AddDays(text.Day) : data.Today.AddDays(text.Day);
            var post = new DemoPost
            {
                Author = By(text.Author),
                Post = new ForumPost
                {
                    ForumCategoryId = text.CategoryId,
                    Title = text.Title,
                    Body = text.Body,
                    Language = Language.English,
                    CreatedAt = DemoTime.Utc(date, text.Hour, text.Minute)
                }
            };

            foreach (var (author, hours, body) in text.Comments)
            {
                var comment = new DemoComment
                {
                    Author = By(author),
                    Comment = new ForumComment
                    {
                        Body = body,
                        CreatedAt = post.Post.CreatedAt.AddHours(hours).AddMinutes(rng.Pick(0, 40))
                    }
                };
                post.Comments.Add(comment);

                // The post's author hears about comments by others (within the 30 days, as above).
                var at = comment.Comment.CreatedAt;
                if (comment.Author != post.Author && at >= data.NowUtc - KeptFor)
                {
                    data.Notifications.Add(new DemoNotification
                    {
                        RecipientEmail = post.Author.Email,
                        Post = post,
                        Comment = comment,
                        Notification = new Notification
                        {
                            Type = NotificationType.CommentOnYourPost,
                            PostTitle = post.Post.Title,
                            OtherName = comment.Author.Profile.DisplayName,
                            CreatedAt = at,
                            IsRead = at < data.NowUtc - UnreadFor
                        }
                    });
                }
            }

            data.Posts.Add(post);
        }

        // Kasia saved Ola's 4am post; Ola reported the blunt comment under it as unkind, still open.
        var fourAm = data.Posts.Single(p => p.Post.Title == DemoTexts.FourAmTitle);
        data.SavedPosts.Add(new DemoSavedPost
        {
            Mother = By(DemoAccounts.KasiaEmail),
            Post = fourAm,
            Saved = new SavedPost { SavedAt = fourAm.Post.CreatedAt.AddHours(26) }
        });

        var unkind = fourAm.Comments.Single(c => c.Comment.Body == DemoTexts.UnkindComment);
        data.CommentReports.Add(new DemoCommentReport
        {
            Reporter = By(DemoAccounts.OlaEmail),
            Comment = unkind,
            Report = new CommentReport
            {
                Reason = ReportReason.Unkind,
                Note = "That felt a bit harsh.",
                Status = ReportStatus.Open,
                CreatedAt = unkind.Comment.CreatedAt.AddMinutes(80)
            }
        });
    }

    // About 40 Assistant searches over the 3 months: most matched with a suggested topic, some without a topic,
    // some not matched, two with the urgent card. Only what the app stores: date, matched, topic, urgent.
    private static void AddAssistantQueries(DemoData data, Random rng)
    {
        var searches = new List<(bool Matched, Topic? Topic, bool Urgent)>();
        void Add(int count, bool matched, Topic? topic)
        {
            for (var i = 0; i < count; i++)
            {
                searches.Add((matched, topic, false));
            }
        }

        Add(9, true, Topic.BabySleep);
        Add(7, true, Topic.BabyFeeding);
        Add(4, true, Topic.PostpartumWellbeing);
        Add(3, true, Topic.EmotionalSupport);
        Add(2, true, Topic.RecoveryAfterBirth);
        Add(2, true, Topic.Nutrition);
        Add(3, true, null);
        Add(8, false, null);
        searches.Add((true, Topic.EmotionalSupport, true));
        searches.Add((false, null, true));

        var span = data.Today.DayNumber - data.FirstDay.DayNumber + 1;
        var shuffled = searches.OrderBy(_ => rng.Next()).ToList();
        for (var i = 0; i < shuffled.Count; i++)
        {
            var day = (int)(span * (i + rng.NextDouble()) / shuffled.Count);
            data.AssistantQueries.Add(new AssistantQuery
            {
                Date = data.FirstDay.AddDays(Math.Min(day, span - 1)),
                Matched = shuffled[i].Matched,
                SuggestedTopic = shuffled[i].Topic,
                UrgentShown = shuffled[i].Urgent
            });
        }
    }

    private static List<DateOnly> Dates(DateOnly from, DateOnly to)
    {
        var dates = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }
}
