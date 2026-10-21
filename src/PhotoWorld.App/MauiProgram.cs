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
        services.AddTransient<ProfilePage>().AddTransient<ProfileViewModel>();
        services.AddTransient<MemberPage>().AddTransient<MemberViewModel>();
        services.AddTransient<PhotoFilterPage>().AddTransient<PhotoFilterViewModel>();
        services.AddTransient<ChallengesPage>().AddTransient<ChallengesViewModel>();
        services.AddTransient<ChallengePage>().AddTransient<ChallengeViewModel>();
        services.AddTransient<EnterChallengePage>().AddTransient<EnterChallengeViewModel>();
        services.AddTransient<ActivityPage>().AddTransient<ActivityViewModel>();

        return builder.Build();
    }
}
