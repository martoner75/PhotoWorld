using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PhotoWorld.App.Services;
using PhotoWorld.Shared.Collections;

namespace PhotoWorld.App.ViewModels;

/// <summary>A collection's name, description or visibility changed: its open page shows the new ones.</summary>
public sealed record CollectionChangedMessage(CollectionSummaryDto Collection);

/// <summary>Starts a collection, or edits one of the member's own (query "id"): name, description, public or private.</summary>
public partial class CollectionEditorViewModel(PhotoWorldApi api) : ViewModelBase, IQueryAttributable
{
    private Guid? _id;

    [ObservableProperty] public partial string Heading { get; set; } = "New collection";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Title { get; set; } = "";

    [ObservableProperty] public partial string Description { get; set; } = "";
    [ObservableProperty] public partial bool IsPublic { get; set; }

    public int TitleMaxLength => CollectionLimits.TitleMax;
    public int DescriptionMaxLength => CollectionLimits.DescriptionMax;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid collectionId)
        {
            _id = collectionId;
            Heading = "Edit collection";
            _ = LoadAsync(collectionId);
        }
    }

    private Task LoadAsync(Guid id) => RunAsync(async () =>
    {
        var c = (await api.GetCollectionAsync(id, 1)).Collection;
        (Title, Description, IsPublic) = (c.Title, c.Description ?? "", c.IsPublic);
    });

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var request = new SaveCollectionRequest(Title.Trim(), string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(), IsPublic);
        if (_id is { } id)
            WeakReferenceMessenger.Default.Send(new CollectionChangedMessage(await api.UpdateCollectionAsync(id, request)));
        else
            await api.CreateCollectionAsync(request);
        await Shell.Current.GoToAsync("..");
    });
}
