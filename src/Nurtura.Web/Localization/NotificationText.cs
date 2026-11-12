using Microsoft.Extensions.Localization;
using Nurtura.Core.Entities;
using Nurtura.Web.Areas.Mother;

namespace Nurtura.Web.Localization;

// Builds a notification's sentence from its type and details, in the current language.
public static class NotificationText
{
    public static string Sentence(IStringLocalizer localizer, Notification notification)
    {
        switch (notification.Type)
        {
            case NotificationType.CommentOnYourPost:
                return localizer["Notification_CommentOnYourPost", notification.OtherName, notification.PostTitle ?? string.Empty];
            case NotificationType.PostHidden or NotificationType.PostRemoved
                or NotificationType.CommentHidden or NotificationType.CommentRemoved:
                return localizer["Notification_" + notification.Type, notification.PostTitle ?? string.Empty];
        }

        var moment = BookText.Moment(localizer, notification.Date!.Value.ToDateTime(new TimeOnly(notification.Hour!.Value, 0)));
        if (notification.Type == NotificationType.Rescheduled
            && notification.PreviousDate is { } previousDate
            && notification.PreviousHour is { } previousHour)
        {
            var previous = BookText.Moment(localizer, previousDate.ToDateTime(new TimeOnly(previousHour, 0)));
            return localizer["Notification_Rescheduled", notification.OtherName, previous, moment];
        }

        return localizer["Notification_" + notification.Type, notification.OtherName, moment];
    }
}
