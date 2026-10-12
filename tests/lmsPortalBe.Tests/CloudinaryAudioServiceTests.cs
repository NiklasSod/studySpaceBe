using lmsPortalBe.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace lmsPortalBe.Tests;

/// <summary>
/// Covers the URL/asset bookkeeping that decides which Cloudinary assets a
/// resource owns. The upload and delete calls themselves are never reached
/// because no API credentials are configured.
/// </summary>
public class CloudinaryAudioServiceTests
{
  private static CloudinaryAudioService CreateService(string? cloudName = "study-space") =>
    new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
              ["CLOUDINARY_CLOUD_NAME"] = cloudName
            })
            .Build(),
        NullLogger<CloudinaryAudioService>.Instance);

  [Theory]
  [InlineData(
      "https://res.cloudinary.com/study-space/raw/upload/v1712345678/voice-notes/lesson.mp3",
      "voice-notes/lesson.mp3")]
  [InlineData(
      "https://res.cloudinary.com/study-space/raw/upload/voice-notes/lesson.mp3",
      "voice-notes/lesson.mp3")]
  [InlineData(
      "https://res.cloudinary.com/study-space/raw/upload/v1/voice-notes/my%20take.m4a",
      "voice-notes/my take.m4a")]
  [InlineData(
      "https://res.cloudinary.com/study-space/raw/upload/v1/lesson.mp3",
      "lesson.mp3")]
  public void GetPublicId_ForRawUploadInThisAccount_ReturnsPublicId(string url, string expected)
  {
    Assert.Equal(expected, CreateService().GetPublicId(url));
  }

  [Theory]
  [InlineData("https://res.cloudinary.com/other-cloud/raw/upload/v1/voice-notes/lesson.mp3")]
  [InlineData("https://res.cloudinary.com/study-space/image/upload/v1/voice-notes/lesson.mp3")]
  [InlineData("https://res.cloudinary.com/study-space/raw/upload")]
  [InlineData("https://res.cloudinary.com/study-space/raw/upload/v1/")]
  [InlineData("http://res.cloudinary.com/study-space/raw/upload/v1/voice-notes/lesson.mp3")]
  [InlineData("https://example.com/voice-notes/lesson.mp3")]
  [InlineData("not-a-url")]
  public void GetPublicId_ForUrlThisAccountDoesNotOwn_ReturnsNull(string url)
  {
    Assert.Null(CreateService().GetPublicId(url));
  }

  [Fact]
  public void GetPublicId_WhenCloudNameIsNotConfigured_ReturnsNull()
  {
    var url = "https://res.cloudinary.com/study-space/raw/upload/v1/voice-notes/lesson.mp3";

    Assert.Null(CreateService(cloudName: null).GetPublicId(url));
  }

  [Fact]
  public async Task DeleteAsync_WhenCredentialsAreMissing_ReturnsFalse()
  {
    // Deleting a recording is best effort: an unconfigured or unreachable
    // Cloudinary account must not fail the request that triggered the delete.
    var deleted = await CreateService().DeleteAsync("voice-notes/lesson.mp3");

    Assert.False(deleted);
  }
}
