using Microsoft.Extensions.Logging;
using PhotoWorld.App.Services;
using PhotoWorld.App.ViewModels;
using PhotoWorld.App.Views;

namespace PhotoWorld.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var services = builder.Services;
        services.AddSingleton<AuthService>();
        services.AddSingleton(sp => new PhotoWorldApi(new HttpClient(
            new ApiAuthHandler(sp.GetRequiredService<AuthService>()) { InnerHandler = AppConfig.CreateHttpHandler() })
        {
            BaseAddress = AppConfig.ApiBaseUrl,
            Timeout = TimeSpan.FromMinutes(2), // photo uploads on a slow connection
        }));

        services.AddSingleton<AppShell>();
        services.AddTransient<LoginPage>().AddTransient<LoginViewModel>();
        services.AddTransient<GalleryPage>().AddTransient<GalleryViewModel>();
        services.AddTransient<PhotoDetailPage>().AddTransient<PhotoDetailViewModel>();
        services.AddTransient<UploadPage>().AddTransient<UploadViewModel>();
        services.AddTransient<BlogsPage>().AddTransient<BlogsViewModel>();
        services.AddTransient<PostPage>().AddTransient<PostViewModel>();
        services.AddTransient<PostCommentsPage>().AddTransient<PostCommentsViewModel>();
        services.AddTransient<PlacesPage>().AddTransient<PlacesViewModel>();
        services.AddTransient<PlaceEditorPage>().AddTransient<PlaceEditorViewModel>();
        // One view model for Me and Settings: like the Me tab itself it lives as long as the app.
        services.AddTransient<ProfilePage>().AddSingleton<ProfileViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<CommunityPage>().AddTransient<CommunityViewModel>();
        services.AddTransient<MemberPage>().AddTransient<MemberViewModel>();
        services.AddTransient<PhotoFilterPage>().AddTransient<PhotoFilterViewModel>();
        services.AddTransient<ChallengesPage>().AddTransient<ChallengesViewModel>();
        services.AddTransient<ChallengePage>().AddTransient<ChallengeViewModel>();
        services.AddTransient<EnterChallengePage>().AddTransient<EnterChallengeViewModel>();
        services.AddTransient<ActivityPage>().AddTransient<ActivityViewModel>();
        services.AddTransient<SpotsPage>().AddTransient<SpotsViewModel>();
        services.AddTransient<CollectionsPage>().AddTransient<CollectionsViewModel>();
        services.AddTransient<CollectionPage>().AddTransient<CollectionViewModel>();
        services.AddTransient<ExplorePage>().AddTransient<ExploreViewModel>();
        services.AddTransient<FeedbackPage>().AddTransient<FeedbackViewModel>();
        services.AddTransient<WalksPage>().AddTransient<WalksViewModel>();
        services.AddTransient<ClubsPage>().AddTransient<ClubsViewModel>();
        services.AddTransient<ClubPage>().AddTransient<ClubViewModel>();
        services.AddTransient<ClubEditorPage>().AddTransient<ClubEditorViewModel>();
        services.AddTransient<WalkPage>().AddTransient<WalkViewModel>();
        services.AddTransient<WalkEditorPage>().AddTransient<WalkEditorViewModel>();
        services.AddTransient<WorkshopsPage>().AddTransient<WorkshopsViewModel>();
        services.AddTransient<WorkshopPage>().AddTransient<WorkshopViewModel>();
        services.AddTransient<WorkshopEditorPage>().AddTransient<WorkshopEditorViewModel>();
        services.AddTransient<InsightsPage>().AddTransient<InsightsViewModel>();
        services.AddTransient<PhotoEditorPage>().AddTransient<PhotoEditorViewModel>();
        services.AddTransient<CollectionEditorPage>().AddTransient<CollectionEditorViewModel>();
        services.AddTransient<PortfolioPage>().AddTransient<PortfolioViewModel>();

        return builder.Build();
    }
}
