using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IHLDota2Bot.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Re-fetch live data for this screen. Called by the shell each time the page becomes
    /// active so freshly-changed server state (e.g. new ratings after a match) shows up.
    /// Default is a no-op for screens that self-refresh or have nothing to reload.
    /// </summary>
    public virtual Task RefreshAsync() => Task.CompletedTask;
}
