namespace SkyrimRpEncyclopedia.Models;

public class Article
{
    public int Id { get; set; }

    public string Title { get; set; } = "";

    public string Category { get; set; } = "";

    public string Summary { get; set; } = "";

    private readonly object _contentLock = new();
    private string _content = "";
    private Func<string>? _contentLoader;
    public string Content
    {
        get
        {
            lock (_contentLock)
            {
                if (_contentLoader is not null)
                {
                    _content = _contentLoader();
                    _contentLoader = null;
                }
                return _content;
            }
        }
        set { lock (_contentLock) { _content = value; _contentLoader = null; } }
    }
    public bool IsContentLoaded { get { lock (_contentLock) return _contentLoader is null; } }
    internal void DeferContent(Func<string> loader) { lock (_contentLock) _contentLoader = loader; }

    public string Tags { get; set; } = "";

    // Additional source categories. Category stays the primary/display
    // category for compatibility with existing screens.
    public IReadOnlyCollection<string> AdditionalCategories { get; set; } = Array.Empty<string>();

    public string Flags { get; set; } = "";

    public bool IsFavorite { get; set; }

    public bool IsOfficial { get; set; }

    // Read-only corpus from Skyrim Knowledge Base V2.  It is intentionally
    // distinct from both official.db and user.db.
    public bool IsMvp { get; set; }
    public bool IsRpRule { get; set; }

    // Present only for read-only imported knowledge articles.  User and
    // official models continue to keep their existing source handling.
    public string SourceUrl { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }


}
