using PhotoWorld.Api.Services.Photos;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Tests;

public class PhotoReviewTests
{
    [Fact]
    public void Members_uploads_wait_for_approval_and_admins_do_not()
    {
        Assert.Equal(PhotoReviewStatus.Pending, PhotoReview.StatusOnUpload(ownerIsAdmin: false));
        Assert.Equal(PhotoReviewStatus.Approved, PhotoReview.StatusOnUpload(ownerIsAdmin: true));
    }

    [Fact]
    public void A_new_before_image_sends_a_members_approved_photo_back_for_approval()
    {
        Assert.Equal(PhotoReviewStatus.Pending, PhotoReview.StatusAfterNewImage(PhotoReviewStatus.Approved, ownerIsAdmin: false));
        Assert.Equal(PhotoReviewStatus.Pending, PhotoReview.StatusAfterNewImage(PhotoReviewStatus.Pending, ownerIsAdmin: false));
        Assert.Equal(PhotoReviewStatus.Approved, PhotoReview.StatusAfterNewImage(PhotoReviewStatus.Approved, ownerIsAdmin: true));
    }

    [Fact]
    public void Only_pending_photos_can_be_reviewed()
    {
        Assert.True(PhotoReview.CanReview(PhotoReviewStatus.Pending));
        Assert.False(PhotoReview.CanReview(PhotoReviewStatus.Approved));
        Assert.False(PhotoReview.CanReview(PhotoReviewStatus.Rejected));
    }
}
