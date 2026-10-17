using PhotoWorld.App.Services;
using PhotoWorld.App.Views;

namespace PhotoWorld.App;

public partial class AppShell : Shell
{
    private readonly AuthService _auth;

    public AppShell(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;

        Routing.RegisterRoute(Routes.Photo, typeof(PhotoDetailPage));
        Routing.RegisterRoute(Routes.Post, typeof(PostPage));
        Routing.RegisterRoute(Routes.PostComments, typeof(PostCommentsPage));
        Routing.RegisterRoute(Routes.PlaceEditor, typeof(PlaceEditorPage));

        // Signed out from anywhere (button, deleted account, expired token): back to the sign-in page.
        auth.SignedOut += (_, _) => MainThread.BeginInvokeOnMainThread(async () => await GoToAsync(Routes.Login, animate: false));
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (await _auth.RestoreAsync()) await GoToAsync(Routes.Gallery, animate: false);
    }
}
