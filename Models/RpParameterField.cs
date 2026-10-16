using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SkyrimRpEncyclopedia.Models;

public sealed class RpParameterField : INotifyPropertyChanged
{
    public const string RandomValue = "Случайно";
    public const string UnspecifiedValue = "Не задано";

    private string _value = RandomValue;

    public required string Key { get; init; }

    public required string Label { get; init; }

    public bool IsOptional { get; init; }

    public required IReadOnlyList<string> Options { get; init; }

    public string Value
    {
        get => _value;
        set
        {
            _value = value ?? string.Empty;
            GeneratedValue = null;
            OnPropertyChanged();
        }
    }

    public string? GeneratedValue { get; private set; }

    public bool IsRandom => string.IsNullOrWhiteSpace(Value) || Value == RandomValue;

    public bool IsUnspecified => IsOptional && Value == UnspecifiedValue;

    public void Reroll(string value)
    {
        _value = RandomValue;
        GeneratedValue = value;
        OnPropertyChanged(nameof(Value));
    }

    public string Resolve(Func<string> chooseRandom)
    {
        if (IsUnspecified)
            return string.Empty;

        if (!IsRandom)
            return Value.Trim();

        GeneratedValue ??= chooseRandom();
        return GeneratedValue;
    }

    public bool IsCustomValue =>
        !IsRandom &&
        !IsUnspecified &&
        !Options.Contains(Value, StringComparer.Ordinal);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
