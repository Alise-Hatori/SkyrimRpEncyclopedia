using System.Globalization;
using SkyrimRpEncyclopedia.Models;

namespace SkyrimRpEncyclopedia.Services;

/// <summary>Separate audiences; neither text includes generator diagnostics or technical identifiers.</summary>
public static class RpSimpleEventTextFormatter
{
    public static string FormatAnnouncement(RpSimpleEvent value) =>
        $"{value.Title}\n\nМесто: {value.Location}\nВремя: {value.TimeDescription}\n" +
        $"Продолжительность: {value.DurationMinutes.ToString(CultureInfo.InvariantCulture)} минут\n\n" +
        $"Повод встречи: {value.GatheringReason}\n\n{value.ParticipantDescription}";

    public static string FormatGmNotes(RpSimpleEvent value) =>
        $"{value.Title}\n\nЗавязка для ведущего\n{value.GmOpening}\n\n" +
        "Идеи начала отыгрыша (необязательные)\n" +
        string.Join("\n", value.OpeningIdeas.Select((idea, index) => $"{index + 1}. {idea}"));
}
