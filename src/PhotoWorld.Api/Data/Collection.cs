namespace PhotoWorld.Api.Data;

/// <summary>A member's collection of photos (their own or other members'). Goes with its owner's account.</summary>
public class Collection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Public collections show on the owner's member page; private ones only to the owner.</summary>
    public bool IsPublic { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<CollectionPhoto> Photos { get; set; } = [];
}

/// <summary>A photo in a collection. Goes when the photo or the collection goes.</summary>
public class CollectionPhoto
{
    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;
    public Guid PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public DateTimeOffset AddedAt { get; set; }
}
