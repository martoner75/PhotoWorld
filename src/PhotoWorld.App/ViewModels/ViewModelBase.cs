using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Runs one action at a time, showing a spinner and any API error.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (ApiException ex)
        {
            await Ui.ShowAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asks for a reason and files the report. Reporting is how members flag objectionable content.</summary>
    protected Task ReportAsync(Func<CreateReportRequest, Task> send) => RunAsync(async () =>
    {
        if (await Ui.PickReportReasonAsync() is not { } reason) return;
        await send(new CreateReportRequest(reason, null));
        await Ui.AlertAsync("Thanks for letting us know. We'll take a look.");
    });
}

/// <summary>A pull-to-refresh list that loads the next page as the member scrolls.</summary>
public abstract partial class PagedListViewModel<T> : ViewModelBase
{
    private int _page;
    private bool _hasNext;
    private bool _loadingMore;

    public ObservableCollection<T> Items { get; } = [];

    /// <summary>Bound to the RefreshView: setting it to true runs <see cref="RefreshCommand"/>.</summary>
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    protected abstract Task<PagedResult<T>> FetchAsync(int page);

    /// <summary>First visit: start loading (the RefreshView shows its spinner and runs the refresh).</summary>
    public void EnsureLoaded()
    {
        if (Items.Count == 0 && !IsRefreshing) IsRefreshing = true;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var first = await FetchAsync(1);
            Items.Clear();
            foreach (var item in first.Items) Items.Add(item);
            (_page, _hasNext) = (1, first.HasNext);
            IsEmpty = Items.Count == 0;
        }
        catch (ApiException ex)
        {
            await Ui.ShowAsync(ex);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (_loadingMore || !_hasNext || IsRefreshing) return;
        _loadingMore = true;
        try
        {
            var next = await FetchAsync(_page + 1);
            foreach (var item in next.Items) Items.Add(item);
            (_page, _hasNext) = (next.Page, next.HasNext);
        }
        catch (ApiException)
        {
            // Scrolling again retries; no need to interrupt with an alert.
        }
        finally
        {
            _loadingMore = false;
        }
    }
}
