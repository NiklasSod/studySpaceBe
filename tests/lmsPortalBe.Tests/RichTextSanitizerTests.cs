using lmsPortalBe.Services;

namespace lmsPortalBe.Tests;

public class RichTextSanitizerTests
{
  private readonly RichTextSanitizer _sanitizer = new();

  [Fact]
  public void Sanitize_PlainText_IsUnchanged()
  {
    var result = _sanitizer.Sanitize("Hello, this is a description.");

    Assert.Equal("Hello, this is a description.", result);
  }

  [Fact]
  public void Sanitize_StripsScriptsAndEventHandlers()
  {
    var result = _sanitizer.Sanitize(
        "<p onclick=\"alert('x')\">Hi</p><script>alert('x')</script>");

    Assert.DoesNotContain("<script", result);
    Assert.DoesNotContain("onclick", result);
    Assert.Contains("Hi", result);
  }

  [Fact]
  public void Sanitize_KeepsAllowedFormatting()
  {
    var result = _sanitizer.Sanitize(
        "<p><strong>Bold</strong> and <em>italic</em><br>line two</p>");

    Assert.Contains("<strong>Bold</strong>", result);
    Assert.Contains("<em>italic</em>", result);
    Assert.Contains("<br>", result);
  }

  [Fact]
  public void Sanitize_KeepsAllowedStyleButDropsDisallowed()
  {
    var result = _sanitizer.Sanitize(
        "<span style=\"color: red; position: absolute; font-size: 20px\">text</span>");

    Assert.Contains("color", result);
    Assert.Contains("font-size", result);
    Assert.DoesNotContain("position", result);
  }

  [Fact]
  public void Sanitize_StripsJavascriptLinks()
  {
    var result = _sanitizer.Sanitize("<a href=\"javascript:alert(1)\">click</a>");

    Assert.DoesNotContain("javascript:", result);
    Assert.Contains("click", result);
  }
}
