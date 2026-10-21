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
        Routing.RegisterRoute(Routes.Member, typeof(MemberPage));
        Routing.RegisterRoute(Routes.PhotoFilter, typeof(PhotoFilterPage));
        Routing.RegisterRoute(Routes.Challenges, typeof(ChallengesPage));
        Routing.RegisterRoute(Routes.Challenge, typeof(ChallengePage));
        Routing.RegisterRoute(Routes.EnterChallenge, typeof(EnterChallengePage));
        Routing.RegisterRoute(Routes.Activity, typeof(ActivityPage));
        Routing.RegisterRoute(Routes.Spots, typeof(SpotsPage));

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
