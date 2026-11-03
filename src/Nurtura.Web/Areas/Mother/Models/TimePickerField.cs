namespace Nurtura.Web.Areas.Mother.Models;

// One time picker field (Views/Shared/_TimePickerField.cshtml). It posts DateName as dd-mm-yyyy and
// TimeName as HH:mm, the same values the date and time model binders read.
// Id is the wrapper's id (a link can jump to it). Optional fields can be cleared (a sleep's end).
public sealed record TimePickerField(
    string Id,
    string Label,
    string DateName,
    string TimeName,
    DateOnly? Date,
    TimeOnly? Time,
    bool Optional = false);
