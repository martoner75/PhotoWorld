using PhotoWorld.Api.Data;
using PhotoWorld.Shared.Photos;

namespace PhotoWorld.Api.Services.Photos;

public static class PhotoFilters
{
    /// <summary>
    /// Narrows photos to the words, tag and camera settings in <paramref name="search"/>. Text uses ToLower().Contains, which EF
    /// turns into a case-insensitive match with the user's text escaped (so % and _ are literal).
    /// </summary>
    public static IQueryable<Photo> Matching(this IQueryable<Photo> photos, PhotoSearch search)
    {
        if (search.Text?.Trim().ToLowerInvariant() is { Length: > 0 } text)
        {
            var asTag = PhotoTags.Normalize(text);
            photos = photos.Where(p => p.Title.ToLower().Contains(text)
                                       || (p.Description != null && p.Description.ToLower().Contains(text))
                                       || p.Tags.Any(t => t.Name == asTag));
        }
        if (PhotoTags.Normalize(search.Tag) is { } tag) photos = photos.Where(p => p.Tags.Any(t => t.Name == tag));
        if (search.Camera?.Trim().ToLowerInvariant() is { Length: > 0 } camera)
            photos = photos.Where(p => (p.CameraMake != null && p.CameraMake.ToLower().Contains(camera))
                                       || (p.CameraModel != null && p.CameraModel.ToLower().Contains(camera)));
        if (search.Lens?.Trim().ToLowerInvariant() is { Length: > 0 } lens)
            photos = photos.Where(p => p.Lens != null && p.Lens.ToLower().Contains(lens));

        if (search.FocalMin is { } focalMin) photos = photos.Where(p => p.FocalLengthMm >= focalMin);
        if (search.FocalMax is { } focalMax) photos = photos.Where(p => p.FocalLengthMm <= focalMax);
        if (search.ApertureMin is { } apertureMin) photos = photos.Where(p => p.Aperture >= apertureMin);
        if (search.ApertureMax is { } apertureMax) photos = photos.Where(p => p.Aperture <= apertureMax);
        if (search.IsoMin is { } isoMin) photos = photos.Where(p => p.Iso >= isoMin);
        if (search.IsoMax is { } isoMax) photos = photos.Where(p => p.Iso <= isoMax);
        return photos;
    }
}
