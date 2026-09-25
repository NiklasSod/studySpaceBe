using Ganss.Xss;

namespace lmsPortalBe.Services
{
  public interface IRichTextSanitizer
  {
    /// <summary>
    /// Sanitizes rich-text (HTML) input so only a small allowlist of
    /// formatting tags, attributes and CSS properties survive. Everything
    /// else (scripts, event handlers, arbitrary styles, ...) is stripped.
    /// </summary>
    string Sanitize(string? html);
  }

  public class RichTextSanitizer : IRichTextSanitizer
  {
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
      "p", "br", "b", "strong", "i", "em", "u", "s",
      "ul", "ol", "li", "h1", "h2", "h3", "h4",
      "a", "span"
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
      "href", "style"
    };

    private static readonly HashSet<string> AllowedCssProperties = new(StringComparer.OrdinalIgnoreCase)
    {
      "color", "background-color", "font-size", "font-family", "text-align"
    };

    private readonly HtmlSanitizer _sanitizer;

    public RichTextSanitizer()
    {
      _sanitizer = new HtmlSanitizer();

      _sanitizer.AllowedTags.Clear();
      foreach (var tag in AllowedTags)
      {
        _sanitizer.AllowedTags.Add(tag);
      }

      _sanitizer.AllowedAttributes.Clear();
      foreach (var attribute in AllowedAttributes)
      {
        _sanitizer.AllowedAttributes.Add(attribute);
      }

      _sanitizer.AllowedCssProperties.Clear();
      foreach (var property in AllowedCssProperties)
      {
        _sanitizer.AllowedCssProperties.Add(property);
      }

      _sanitizer.AllowedSchemes.Clear();
      _sanitizer.AllowedSchemes.Add("http");
      _sanitizer.AllowedSchemes.Add("https");
      _sanitizer.AllowedSchemes.Add("mailto");
    }

    public string Sanitize(string? html) => _sanitizer.Sanitize(html ?? string.Empty);
  }
}
