using Microsoft.EntityFrameworkCore;
using Nurtura.Core.Entities;

namespace Nurtura.Core.Services;

// Every method takes the logged-in user's id and works only on that user's own check-ins.
// Check-ins are found by date, never by id, so no other mother's check-in can be reached.
public class DailyCheckInService
{
    private readonly INurturaDbContext _db;

    public DailyCheckInService(INurturaDbContext db)
    {
        _db = db;
    }

    // Today (in Poland) and the EditableDays before it.
    public static bool IsEditableDate(DateOnly date, DateOnly today) =>
        date <= today && date >= today.AddDays(-DailyCheckIn.EditableDays);

    public async Task<IReadOnlyList<FeelingTag>> GetFeelingTagsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.FeelingTags
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    // Null when she has no check-in for that date.
    public async Task<DailyCheckIn?> GetAsync(string userId, DateOnly date, CancellationToken cancellationToken = default)
    {
        return await _db.DailyCheckIns
            .AsNoTracking()
            .Include(c => c.FeelingTags)
            .Where(c => c.Date == date
                && _db.MotherProfiles.Any(p => p.Id == c.MotherProfileId && p.UserId == userId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Creates her check-in for the date, or updates it if that date already has one.
    // Returns false when the user has no mother profile.
    public async Task<bool> SaveAsync(string userId, CheckInAnswers answers, CancellationToken cancellationToken = default)
    {
        if (!IsEditableDate(answers.Date, PolandTime.Today))
        {
            throw new ArgumentException("The date must be today or one of the past days that can still be edited.", nameof(answers));
        }

        foreach (var rating in new[] { answers.Mood, answers.SleepQuality, answers.Energy, answers.Stress })
        {
            if (rating is < DailyCheckIn.RatingMin or > DailyCheckIn.RatingMax)
            {
                throw new ArgumentException("A rating is out of range.", nameof(answers));
            }
        }

        // Stored with "\n" line breaks, so "\r\n" counts as one character towards the limit.
        var note = string.IsNullOrWhiteSpace(answers.Note) ? null : answers.Note.ReplaceLineEndings("\n").Trim();
        if (note?.Length > DailyCheckIn.NoteMaxLength)
        {
            throw new ArgumentException("The note is too long.", nameof(answers));
        }

        var tagIds = answers.FeelingTagIds.Distinct().ToList();
        var knownTagCount = await _db.FeelingTags.CountAsync(t => tagIds.Contains(t.Id), cancellationToken);
        if (knownTagCount != tagIds.Count)
        {
            throw new ArgumentException("Unknown feeling tag.", nameof(answers));
        }

        var profileId = await _db.MotherProfiles
            .Where(p => p.UserId == userId)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileId is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var checkIn = await _db.DailyCheckIns
            .Include(c => c.FeelingTags)
            .SingleOrDefaultAsync(c => c.MotherProfileId == profileId && c.Date == answers.Date, cancellationToken);
        if (checkIn is null)
        {
            checkIn = new DailyCheckIn
            {
                MotherProfileId = profileId.Value,
                Date = answers.Date,
                CreatedAt = now
            };
            _db.DailyCheckIns.Add(checkIn);
        }

        checkIn.Mood = answers.Mood;
        checkIn.SleepQuality = answers.SleepQuality;
        checkIn.Energy = answers.Energy;
        checkIn.Stress = answers.Stress;
        checkIn.Note = note;
        checkIn.IsShared = answers.IsShared;
        checkIn.UpdatedAt = now;

        checkIn.FeelingTags.RemoveAll(t => !tagIds.Contains(t.FeelingTagId));
        var addedTags = tagIds
            .Where(id => checkIn.FeelingTags.All(t => t.FeelingTagId != id))
            .Select(id => new CheckInFeelingTag { FeelingTagId = id })
            .ToList();
        checkIn.FeelingTags.AddRange(addedTags);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
