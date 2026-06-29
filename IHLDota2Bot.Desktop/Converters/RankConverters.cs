using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using IHLDota2Bot.Desktop.Models;

namespace IHLDota2Bot.Desktop.Converters;

/// <summary>Maps an integer rating to its <see cref="RankTier"/> display name (e.g. "Legend").</summary>
public sealed class RatingToTierNameConverter : IValueConverter
{
    public static readonly RatingToTierNameConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => RankTier.For(value is int r ? r : 0).Name;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Maps an integer rating to its tier accent brush.</summary>
public sealed class RatingToTierBrushConverter : IValueConverter
{
    public static readonly RatingToTierBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new SolidColorBrush(Color.Parse(RankTier.For(value is int r ? r : 0).ColorHex));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true → win green, false → loss red. Used for team-result colouring.</summary>
public sealed class BoolToWinLossBrushConverter : IValueConverter
{
    public static readonly BoolToWinLossBrushConverter Instance = new();
    private static readonly SolidColorBrush Win = new(Color.Parse("#3FD6A0"));
    private static readonly SolidColorBrush Loss = new(Color.Parse("#FF5C5C"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Win : Loss;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
