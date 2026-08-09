using CommunityToolkit.Mvvm.Input;
using Illumination.Application.Study;

namespace Illumination.Desktop;

public sealed record StudyAssessmentPreviewDisplay(
    StudyLearningAssessment Assessment,
    string Name,
    string Projection,
    IAsyncRelayCommand Command);

public sealed record StudyQueueEntryDisplay(
    string Prompt,
    bool ReinforcementRequired,
    string StateLabel);

public static class StudyPresentationFormatter
{
    public static string FormatPreview(StudyAssessmentPreview preview, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(preview);

        if (preview.ProjectedDueAt is { } dueAt)
        {
            return FormatDuration(dueAt - now);
        }

        if (preview.Assessment == StudyLearningAssessment.Unsicher && preview.ProjectedInterveningEntryCount > 0)
        {
            return "end of stack";
        }

        var intervening = preview.ProjectedInterveningEntryCount ?? 0;
        return intervening == 0
            ? "again immediately"
            : $"after {intervening} {(intervening == 1 ? "card" : "cards")}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return "now";

        var totalDays = duration.TotalDays;
        if (totalDays >= 14)
        {
            var weeks = Math.Max(1, (int)Math.Round(totalDays / 7, MidpointRounding.AwayFromZero));
            return $"{weeks} {(weeks == 1 ? "week" : "weeks")}";
        }

        if (totalDays >= 1)
        {
            var days = Math.Max(1, (int)Math.Round(totalDays, MidpointRounding.AwayFromZero));
            return $"{days} {(days == 1 ? "day" : "days")}";
        }

        if (duration.TotalHours >= 1)
        {
            var hours = Math.Max(1, (int)Math.Round(duration.TotalHours, MidpointRounding.AwayFromZero));
            return $"{hours} {(hours == 1 ? "hour" : "hours")}";
        }

        var minutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
        return $"{minutes} {(minutes == 1 ? "minute" : "minutes")}";
    }
}
