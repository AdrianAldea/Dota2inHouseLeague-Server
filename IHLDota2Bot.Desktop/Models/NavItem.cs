using Avalonia.Media;
using IHLDota2Bot.Desktop.ViewModels;

namespace IHLDota2Bot.Desktop.Models;

/// <summary>One entry in the left sidebar: a label, a vector icon, and the page it shows.</summary>
public sealed class NavItem
{
    public required string Title { get; init; }
    public required Geometry Icon { get; init; }
    public required ViewModelBase Page { get; init; }
}
