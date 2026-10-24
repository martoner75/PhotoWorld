using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Services.Photos;

/// <summary>The approval rules for photos (see <see cref="PhotoReviewStatus"/>), kept apart so they're easy to test.</summary>
public static class PhotoReview
{
    /// <summary>A new upload waits for an admin, unless an admin uploaded it.</summary>
    public static string StatusOnUpload(bool ownerIsAdmin) =>
        ownerIsAdmin ? PhotoReviewStatus.Approved : PhotoReviewStatus.Pending;

    /// <summary>
    /// Adding a new image (a "before" version) to an approved photo sends it back to the queue, unless an admin owns
    /// it: approval was for the image that was there. Pending photos stay pending.
    /// </summary>
    public static string StatusAfterNewImage(string current, bool ownerIsAdmin) =>
        ownerIsAdmin || current != PhotoReviewStatus.Approved ? current : PhotoReviewStatus.Pending;

    /// <summary>Only photos waiting in the queue can be approved or rejected.</summary>
    public static bool CanReview(string status) => status == PhotoReviewStatus.Pending;
}
