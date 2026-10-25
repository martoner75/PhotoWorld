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
        Routing.RegisterRoute(Routes.Collections, typeof(CollectionsPage));
        Routing.RegisterRoute(Routes.Collection, typeof(CollectionPage));
        Routing.RegisterRoute(Routes.Explore, typeof(ExplorePage));
        Routing.RegisterRoute(Routes.Feedback, typeof(FeedbackPage));
        Routing.RegisterRoute(Routes.Walks, typeof(WalksPage));
        Routing.RegisterRoute(Routes.Walk, typeof(WalkPage));
        Routing.RegisterRoute(Routes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(Routes.Upload, typeof(UploadPage));
        Routing.RegisterRoute(Routes.Clubs, typeof(ClubsPage));
        Routing.RegisterRoute(Routes.Club, typeof(ClubPage));
        Routing.RegisterRoute(Routes.ClubEditor, typeof(ClubEditorPage));
        Routing.RegisterRoute(Routes.WalkEditor, typeof(WalkEditorPage));
        Routing.RegisterRoute(Routes.Insights, typeof(InsightsPage));
        Routing.RegisterRoute(Routes.PhotoEditor, typeof(PhotoEditorPage));
        Routing.RegisterRoute(Routes.CollectionEditor, typeof(CollectionEditorPage));
        Routing.RegisterRoute(Routes.Portfolio, typeof(PortfolioPage));

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
