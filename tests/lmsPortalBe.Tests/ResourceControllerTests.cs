using System.Net;
using System.Net.Http.Json;
using lmsPortalBe.DTOs.Admin;
using lmsPortalBe.DTOs.Auth;
using lmsPortalBe.DTOs.Course;
using lmsPortalBe.DTOs.Resource;
using lmsPortalBe.Services;
using Microsoft.Extensions.DependencyInjection;

namespace lmsPortalBe.Tests;

public class ResourceControllerTests : ApiTestBase, IClassFixture<TestWebApplicationFactory>
{
  public ResourceControllerTests(TestWebApplicationFactory factory) : base(factory)
  {
  }

  private RecordingCloudinaryAudioService CloudinaryAudio =>
      (RecordingCloudinaryAudioService)Factory.Services.GetRequiredService<ICloudinaryAudioService>();

  private static readonly DateTime Jan1 = new(2026, 1, 1);
  private static readonly DateTime Jan31 = new(2026, 1, 31);

  private async Task<AuthResponseDto> CreateTeacherAsync(string email)
  {
    await RegisterAsync(email);

    var admin = await LoginAsync("admin@example.com", "AdminPass1");
    var promote = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/admin/assign-role",
        admin.AccessToken,
        new AssignRoleRequestDto { Email = email, Role = "teacher" });
    promote.EnsureSuccessStatusCode();

    return await LoginAsync(email, "Passw0rd1");
  }

  private async Task<int> CreateCourseAsync(string teacherToken)
  {
    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/courses",
        teacherToken,
        new CreateCourseRequestDto
        {
          Name = "Resource test course",
          Description = "Course for resource tests",
          StartDate = Jan1,
          EndDate = Jan31
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<CourseSummaryDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    return body.Id;
  }

  private async Task<int> CreateModuleAsync(string teacherToken, int courseId)
  {
    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/modules",
        teacherToken,
        new CreateCourseModuleRequestDto
        {
          CourseId = courseId,
          Name = "Resource test module",
          Description = "Module for resource tests",
          StartDate = Jan1,
          EndDate = Jan31
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<CourseModuleSummaryDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    return body.Id;
  }

  private async Task<int> CreateActivityAsync(string teacherToken, int moduleId)
  {
    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/activities",
        teacherToken,
        new CreateActivityRequestDto
        {
          ModuleId = moduleId,
          Type = "Lecture",
          Name = "Resource test activity",
          Description = "Activity for resource tests",
          StartDate = Jan1,
          EndDate = Jan31
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ActivityDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    return body.Id;
  }

  private async Task<int> CreateResourceAsync(string teacherToken, CreateResourceRequestDto dto)
  {
    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacherToken,
        dto);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    return body.Id;
  }

  private async Task EnrollAsync(string studentToken, string teacherToken, int courseId, string studentEmail)
  {
    await EnrollAndApproveAsync(studentToken, teacherToken, courseId, studentEmail);
  }

  [Fact]
  public async Task CreateResource_UnderCourse_AsTeacher_ReturnsCreatedWithId()
  {
    var teacher = await CreateTeacherAsync("resource.create.course@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Slides",
          Url = "https://example.com/slides.pdf",
          CourseId = courseId
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.NotEqual(0, body.Id);
    Assert.Equal("Slides", body.DisplayName);
    Assert.Equal(courseId, body.CourseId);
  }

  [Fact]
  public async Task CreateResource_UnderModule_AsTeacher_ReturnsCreatedWithId()
  {
    var teacher = await CreateTeacherAsync("resource.create.module@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Reading",
          Url = "https://example.com/reading.pdf",
          ModuleId = moduleId
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.NotEqual(0, body.Id);
    Assert.Equal(moduleId, body.ModuleId);
  }

  [Fact]
  public async Task CreateResource_UnderActivity_AsTeacher_ReturnsCreatedWithId()
  {
    var teacher = await CreateTeacherAsync("resource.create.activity@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);
    var activityId = await CreateActivityAsync(teacher.AccessToken, moduleId);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Lab handout",
          Url = "https://example.com/lab.pdf",
          ActivityId = activityId
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.NotEqual(0, body.Id);
    Assert.Equal(activityId, body.ActivityId);
  }

  [Fact]
  public async Task CreateResource_WithNoParent_ReturnsBadRequest()
  {
    var teacher = await CreateTeacherAsync("resource.create.noparent@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Orphan",
          Url = "https://example.com/orphan.pdf"
        });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateResource_WithTwoParents_ReturnsBadRequest()
  {
    var teacher = await CreateTeacherAsync("resource.create.twoparents@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Confused",
          Url = "https://example.com/confused.pdf",
          CourseId = courseId,
          ModuleId = moduleId
        });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateResource_WithDescription_SanitizesHtml()
  {
    var teacher = await CreateTeacherAsync("resource.create.description@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Slides",
          Url = "https://example.com/slides.pdf",
          CourseId = courseId,
          Description = "<p>Use <strong>these</strong> slides</p><script>alert('x')</script>"
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Contains("<strong>these</strong>", body.Description);
    Assert.DoesNotContain("<script", body.Description);
  }

  [Fact]
  public async Task UpdateResource_Description_IsSanitized()
  {
    var teacher = await CreateTeacherAsync("resource.update.description@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Patch,
        $"/api/resources/{resourceId}",
        teacher.AccessToken,
        new UpdateResourceRequestDto
        {
          Description = "<p>Updated <em>notes</em></p><script>alert('x')</script>"
        });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Contains("<em>notes</em>", body.Description);
    Assert.DoesNotContain("<script", body.Description);
  }

  [Fact]
  public async Task CreateResource_WithAudioUrls_StoresThemInOrder()
  {
    var teacher = await CreateTeacherAsync("resource.create.audio@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Slides",
          Url = "https://example.com/slides.pdf",
          CourseId = courseId,
          AudioUrls =
          [
            "https://res.cloudinary.com/demo/video/upload/voice-1.mp3",
            "https://res.cloudinary.com/demo/video/upload/voice-2.mp3"
          ]
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Equal(2, body.AudioUrls.Count);
    Assert.Contains(body.AudioUrls, u => u.EndsWith("voice-1.mp3"));
    Assert.Contains(body.AudioUrls, u => u.EndsWith("voice-2.mp3"));
  }

  [Fact]
  public async Task CreateResource_WithNonCloudinaryAudioUrl_ReturnsBadRequest()
  {
    var teacher = await CreateTeacherAsync("resource.create.audio.bad@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Slides",
          Url = "https://example.com/slides.pdf",
          CourseId = courseId,
          AudioUrls = ["https://evil.example.com/voice.mp3"]
        });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task UpdateResource_AudioUrls_ReplacesList()
  {
    var teacher = await CreateTeacherAsync("resource.update.audio@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Patch,
        $"/api/resources/{resourceId}",
        teacher.AccessToken,
        new UpdateResourceRequestDto
        {
          AudioUrls = ["https://res.cloudinary.com/demo/video/upload/voice-1.mp3"]
        });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Single(body.AudioUrls);

    // Sending an empty list clears the audio clips.
    var clear = await SendAuthorizedAsync(
        HttpMethod.Patch,
        $"/api/resources/{resourceId}",
        teacher.AccessToken,
        new UpdateResourceRequestDto { AudioUrls = [] });

    Assert.Equal(HttpStatusCode.OK, clear.StatusCode);

    var cleared = await clear.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(cleared);
    Assert.Empty(cleared.AudioUrls);
  }

  [Fact]
  public async Task CreateResource_InteractiveImage_StoresPoints()
  {
    var teacher = await CreateTeacherAsync("resource.create.image@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Anatomy diagram",
          Url = "https://example.com/anatomy.png",
          CourseId = courseId,
          IsInteractiveImage = true,
          Points =
          [
            new ImagePointDto { X = 0.25, Y = 0.5, Text = "Heart", AudioUrl = "https://res.cloudinary.com/demo/raw/upload/heart.mp3" },
            new ImagePointDto { X = 0.75, Y = 0.2, Text = "Lung", AudioUrl = "https://res.cloudinary.com/demo/raw/upload/lung.mp3" }
          ]
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.True(body.IsInteractiveImage);
    Assert.Equal(2, body.Points.Count);

    var heart = body.Points[0];
    Assert.Equal(0.25, heart.X);
    Assert.Equal(0.5, heart.Y);
    Assert.Equal("Heart", heart.Text);
    Assert.Equal("https://res.cloudinary.com/demo/raw/upload/heart.mp3", heart.AudioUrl);
  }

  [Fact]
  public async Task CreateResource_InteractiveImage_PointOutsideRange_ReturnsBadRequest()
  {
    var teacher = await CreateTeacherAsync("resource.create.image.range@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        teacher.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Anatomy diagram",
          Url = "https://example.com/anatomy.png",
          CourseId = courseId,
          IsInteractiveImage = true,
          Points = [new ImagePointDto { X = 1.5, Y = 0.5, Text = "Off the edge" }]
        });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task UpdateResource_InteractiveImage_SetsPointsAndFlag()
  {
    var teacher = await CreateTeacherAsync("resource.update.image@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Patch,
        $"/api/resources/{resourceId}",
        teacher.AccessToken,
        new UpdateResourceRequestDto
        {
          IsInteractiveImage = true,
          Points = [new ImagePointDto { X = 0.5, Y = 0.5, Text = "Center", AudioUrl = "https://res.cloudinary.com/demo/raw/upload/center.mp3" }]
        });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.True(body.IsInteractiveImage);
    var point = Assert.Single(body.Points);
    Assert.Equal(0.5, point.X);
    Assert.Equal("Center", point.Text);

    // Clearing the points and the flag returns it to a plain resource.
    var clear = await SendAuthorizedAsync(
        HttpMethod.Patch,
        $"/api/resources/{resourceId}",
        teacher.AccessToken,
        new UpdateResourceRequestDto { IsInteractiveImage = false, Points = [] });

    Assert.Equal(HttpStatusCode.OK, clear.StatusCode);

    var cleared = await clear.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(cleared);
    Assert.False(cleared.IsInteractiveImage);
    Assert.Empty(cleared.Points);
  }

  [Fact]
  public async Task UploadAudio_AsStudent_ReturnsForbidden()
  {
    var student = await RegisterAsync("resource.audio.student@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources/audio",
        student.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task UploadAudio_AsTeacher_WithoutFile_ReturnsBadRequest()
  {
    var teacher = await CreateTeacherAsync("resource.audio.teacher@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources/audio",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task UpdateResource_ReplacingOneOfManyRecordings_DeletesOnlyTheReplacedRecording()
  {
    var teacher = await CreateTeacherAsync("resource.audio.replace.one@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var recordings = Enumerable.Range(1, 5)
        .Select(i => $"https://res.cloudinary.com/demo/raw/upload/v17000200{i}/voice-notes/replace-one-take-{i}.mp3")
        .ToList();
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Five takes",
      Url = "https://example.com/notes.pdf",
      CourseId = courseId,
      AudioUrls = recordings
    });

    // The client re-sends the whole list with only the third recording swapped
    // for the freshly uploaded take.
    var reRecorded = "https://res.cloudinary.com/demo/raw/upload/v17000299/voice-notes/replace-one-take-3-redo.mp3";
    var updated = recordings.ToList();
    updated[2] = reRecorded;

    var response = await SendAuthorizedAsync(
        HttpMethod.Patch,
        $"/api/resources/{resourceId}",
        teacher.AccessToken,
        new UpdateResourceRequestDto { AudioUrls = updated });

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Equal(updated, body.AudioUrls);

    // Only the replaced take is destroyed; the other four are left untouched.
    Assert.Equal(["voice-notes/replace-one-take-3.mp3"], DeletedRecordings("voice-notes/replace-one-"));
  }

  [Fact]
  public async Task DeleteResourceAudio_RemovesRecordingFromAudioList()
  {
    var teacher = await CreateTeacherAsync("resource.audio.delete@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var keptUrl = "https://res.cloudinary.com/demo/raw/upload/v1700000000/voice-notes/audio-list-kept.mp3";
    var removedUrl = "https://res.cloudinary.com/demo/raw/upload/v1700000001/voice-notes/audio-list-removed.mp3";
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Voice notes",
      Url = "https://example.com/notes.pdf",
      CourseId = courseId,
      AudioUrls = [keptUrl, removedUrl]
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/resources/{resourceId}/audio?url={Uri.EscapeDataString(removedUrl)}",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var get = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/resources/{resourceId}",
        teacher.AccessToken);
    var body = await get.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Equal(keptUrl, Assert.Single(body.AudioUrls));

    Assert.Equal(["voice-notes/audio-list-removed.mp3"], DeletedRecordings("voice-notes/audio-list-"));
  }

  [Fact]
  public async Task DeleteResourceAudio_RemovesRecordingFromImagePoint()
  {
    var teacher = await CreateTeacherAsync("resource.audio.point.delete@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var keptUrl = "https://res.cloudinary.com/demo/raw/upload/v1700000002/voice-notes/point-audio-kept.mp3";
    var removedUrl = "https://res.cloudinary.com/demo/raw/upload/v1700000003/voice-notes/point-audio-removed.mp3";
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Anatomy",
      Url = "https://example.com/anatomy.png",
      CourseId = courseId,
      IsInteractiveImage = true,
      Points =
      [
        new ImagePointDto { X = 0.25, Y = 0.5, Text = "Heart", AudioUrl = removedUrl },
        new ImagePointDto { X = 0.75, Y = 0.2, Text = "Lung", AudioUrl = keptUrl }
      ]
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/resources/{resourceId}/audio?url={Uri.EscapeDataString(removedUrl)}",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var get = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/resources/{resourceId}",
        teacher.AccessToken);
    var body = await get.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.Equal(2, body.Points.Count);
    Assert.Equal(string.Empty, body.Points[0].AudioUrl);
    Assert.Equal("Heart", body.Points[0].Text);
    Assert.Equal(keptUrl, body.Points[1].AudioUrl);

    Assert.Equal(["voice-notes/point-audio-removed.mp3"], DeletedRecordings("voice-notes/point-audio-"));
  }

  [Fact]
  public async Task DeleteResourceAudio_WithRecordingNotOnResource_ReturnsNotFoundAndDeletesNothing()
  {
    var teacher = await CreateTeacherAsync("resource.audio.unrelated@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId,
      AudioUrls = ["https://res.cloudinary.com/demo/raw/upload/v1700000004/voice-notes/unrelated-slides.mp3"]
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/resources/{resourceId}/audio?url={Uri.EscapeDataString("https://res.cloudinary.com/demo/raw/upload/v1700000005/voice-notes/unrelated-other.mp3")}",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Empty(DeletedRecordings("voice-notes/unrelated-"));
  }

  [Fact]
  public async Task DeleteResourceAudio_AsStudent_ReturnsForbiddenAndDeletesNothing()
  {
    var teacher = await CreateTeacherAsync("resource.audio.delete.student@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var audioUrl = "https://res.cloudinary.com/demo/raw/upload/v1700000006/voice-notes/student-guard.mp3";
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Voice notes",
      Url = "https://example.com/notes.pdf",
      CourseId = courseId,
      AudioUrls = [audioUrl]
    });

    var student = await RegisterAsync("resource.audio.delete.forbidden@example.com");
    var response = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/resources/{resourceId}/audio?url={Uri.EscapeDataString(audioUrl)}",
        student.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Empty(DeletedRecordings("voice-notes/student-guard"));
  }

  [Fact]
  public async Task DeleteResource_DeletesEveryRecordingItOwns()
  {
    var teacher = await CreateTeacherAsync("resource.audio.delete.resource@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var firstUrl = "https://res.cloudinary.com/demo/raw/upload/v1700000007/voice-notes/resource-delete-1.mp3";
    var secondUrl = "https://res.cloudinary.com/demo/raw/upload/v1700000008/voice-notes/resource-delete-2.mp3";
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Voice notes",
      Url = "https://example.com/notes.pdf",
      CourseId = courseId,
      AudioUrls = [firstUrl, secondUrl]
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/resources/{resourceId}",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var deleted = DeletedRecordings("voice-notes/resource-delete-");
    deleted.Sort(StringComparer.Ordinal);
    Assert.Equal(["voice-notes/resource-delete-1.mp3", "voice-notes/resource-delete-2.mp3"], deleted);
  }

  /// <summary>
  /// The audio assets the API asked Cloudinary to delete for one test, matched by
  /// the public IDs that test uploaded so other tests in the class cannot leak in.
  /// </summary>
  private List<string> DeletedRecordings(string publicIdPrefix) =>
      [.. CloudinaryAudio.DeletedPublicIds.Where(id => id.StartsWith(publicIdPrefix, StringComparison.Ordinal))];


  [Fact]
  public async Task GetCourseResources_AsAdmin_ReturnsResources()
  {
    var teacher = await CreateTeacherAsync("resource.list.course.admin@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId
    });

    var admin = await LoginAsync("admin@example.com", "AdminPass1");
    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/courses/{courseId}/resources",
        admin.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.Contains(resources, r => r.Id == resourceId);
  }

  [Fact]
  public async Task GetCourseResources_AsEnrolledStudent_ReturnsResources()
  {
    var teacher = await CreateTeacherAsync("resource.list.course.enrolled@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId
    });

    var student = await RegisterAsync("resource.student.enrolled@example.com");
    await EnrollAsync(student.AccessToken, teacher.AccessToken, courseId, "resource.student.enrolled@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/courses/{courseId}/resources",
        student.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.Contains(resources, r => r.Id == resourceId);
  }

  [Fact]
  public async Task GetCourseResources_AsNonEnrolledStudent_ReturnsForbidden()
  {
    var teacher = await CreateTeacherAsync("resource.list.course.foreign@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId
    });

    var outsider = await RegisterAsync("resource.student.foreign@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/courses/{courseId}/resources",
        outsider.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task GetModuleResources_AsTeacher_ReturnsResources()
  {
    var teacher = await CreateTeacherAsync("resource.list.module@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Reading",
      Url = "https://example.com/reading.pdf",
      ModuleId = moduleId
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/modules/{moduleId}/resources",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.Contains(resources, r => r.Id == resourceId);
  }

  [Fact]
  public async Task GetActivityResources_AsTeacher_ReturnsResources()
  {
    var teacher = await CreateTeacherAsync("resource.list.activity@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);
    var activityId = await CreateActivityAsync(teacher.AccessToken, moduleId);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Lab handout",
      Url = "https://example.com/lab.pdf",
      ActivityId = activityId
    });

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/activity/{activityId}/resources",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.Contains(resources, r => r.Id == resourceId);
  }

  [Fact]
  public async Task GetCourseResources_UnknownCourse_ReturnsNotFound()
  {
    var teacher = await CreateTeacherAsync("resource.list.course.missing@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        "/api/courses/999999/resources",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task DeleteCourse_CascadeDeletesResources()
  {
    var teacher = await CreateTeacherAsync("resource.delete.course@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Slides",
      Url = "https://example.com/slides.pdf",
      CourseId = courseId
    });

    var delete = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/courses/{courseId}",
        teacher.AccessToken);
    Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

    var admin = await LoginAsync("admin@example.com", "AdminPass1");
    var get = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/resources/{resourceId}",
        admin.AccessToken);
    Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
  }

  [Fact]
  public async Task DeleteModule_CascadeDeletesResources()
  {
    var teacher = await CreateTeacherAsync("resource.delete.module@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Reading",
      Url = "https://example.com/reading.pdf",
      ModuleId = moduleId
    });

    var delete = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/modules/{moduleId}",
        teacher.AccessToken);
    Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

    var admin = await LoginAsync("admin@example.com", "AdminPass1");
    var get = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/resources/{resourceId}",
        admin.AccessToken);
    Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
  }

  [Fact]
  public async Task DeleteActivity_CascadeDeletesResources()
  {
    var teacher = await CreateTeacherAsync("resource.delete.activity@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);
    var activityId = await CreateActivityAsync(teacher.AccessToken, moduleId);
    var resourceId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Lab handout",
      Url = "https://example.com/lab.pdf",
      ActivityId = activityId
    });

    var delete = await SendAuthorizedAsync(
        HttpMethod.Delete,
        $"/api/activities/{activityId}",
        teacher.AccessToken);
    Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

    var admin = await LoginAsync("admin@example.com", "AdminPass1");
    var get = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/resources/{resourceId}",
        admin.AccessToken);
    Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
  }

  private async Task<int> CreateStudentResourceAsync(string studentToken, int moduleId, string name)
  {
    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        studentToken,
        new CreateResourceRequestDto
        {
          DisplayName = name,
          Url = $"https://example.com/{name}.pdf",
          ModuleId = moduleId
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    return body.Id;
  }

  [Fact]
  public async Task CreateResource_AsEnrolledStudent_UnderModule_ReturnsCreatedAndFlagged()
  {
    var teacher = await CreateTeacherAsync("resource.student.submit@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var student = await RegisterAsync("resource.student.submit.student@example.com");
    await EnrollAsync(student.AccessToken, teacher.AccessToken, courseId, "resource.student.submit.student@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        student.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "My essay",
          Url = "https://example.com/essay.pdf",
          ModuleId = moduleId
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<ResourceDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.NotEqual(0, body.Id);
    Assert.Equal(moduleId, body.ModuleId);
    Assert.True(body.IsStudentSubmitted);
  }

  [Fact]
  public async Task CreateResource_AsStudent_UnderCourse_ReturnsBadRequest()
  {
    var teacher = await CreateTeacherAsync("resource.student.course@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    var student = await RegisterAsync("resource.student.course.student@example.com");
    await EnrollAsync(student.AccessToken, teacher.AccessToken, courseId, "resource.student.course.student@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        student.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Wrong place",
          Url = "https://example.com/wrong.pdf",
          CourseId = courseId
        });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task CreateResource_AsNonEnrolledStudent_ReturnsForbidden()
  {
    var teacher = await CreateTeacherAsync("resource.student.notenrolled@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var student = await RegisterAsync("resource.student.notenrolled.student@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/resources",
        student.AccessToken,
        new CreateResourceRequestDto
        {
          DisplayName = "Intruder",
          Url = "https://example.com/intruder.pdf",
          ModuleId = moduleId
        });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task GetModuleResources_ExcludesStudentSubmissions()
  {
    var teacher = await CreateTeacherAsync("resource.module.exclude@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var normalId = await CreateResourceAsync(teacher.AccessToken, new CreateResourceRequestDto
    {
      DisplayName = "Course material",
      Url = "https://example.com/material.pdf",
      ModuleId = moduleId
    });

    var student = await RegisterAsync("resource.module.exclude.student@example.com");
    await EnrollAsync(student.AccessToken, teacher.AccessToken, courseId, "resource.module.exclude.student@example.com");
    var studentBody = await CreateStudentResourceAsync(student.AccessToken, moduleId, "Student upload");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/modules/{moduleId}/resources",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.Contains(resources, r => r.Id == normalId);
    Assert.DoesNotContain(resources, r => r.Id == studentBody);
  }

  [Fact]
  public async Task GetModuleStudentResources_AsTeacher_ReturnsAllLatestFirst()
  {
    var teacher = await CreateTeacherAsync("resource.studentlist.teacher@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var studentA = await RegisterAsync("resource.studentlist.a@example.com");
    await EnrollAsync(studentA.AccessToken, teacher.AccessToken, courseId, "resource.studentlist.a@example.com");
    var studentB = await RegisterAsync("resource.studentlist.b@example.com");
    await EnrollAsync(studentB.AccessToken, teacher.AccessToken, courseId, "resource.studentlist.b@example.com");

    var firstId = await CreateStudentResourceAsync(studentA.AccessToken, moduleId, "First upload");
    var secondId = await CreateStudentResourceAsync(studentB.AccessToken, moduleId, "Second upload");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/modules/{moduleId}/student-resources",
        teacher.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.Contains(resources, r => r.Id == firstId);
    Assert.Contains(resources, r => r.Id == secondId);

    var ids = resources.Select(r => r.Id).ToList();
    Assert.True(ids.IndexOf(secondId) < ids.IndexOf(firstId), "Latest submission should be listed first.");
  }

  [Fact]
  public async Task GetModuleStudentResources_AsStudent_ReturnsOnlyOwn()
  {
    var teacher = await CreateTeacherAsync("resource.studentlist.own@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var studentA = await RegisterAsync("resource.studentlist.own.a@example.com");
    await EnrollAsync(studentA.AccessToken, teacher.AccessToken, courseId, "resource.studentlist.own.a@example.com");
    var studentB = await RegisterAsync("resource.studentlist.own.b@example.com");
    await EnrollAsync(studentB.AccessToken, teacher.AccessToken, courseId, "resource.studentlist.own.b@example.com");

    var ownId = await CreateStudentResourceAsync(studentA.AccessToken, moduleId, "A upload");
    var otherId = await CreateStudentResourceAsync(studentB.AccessToken, moduleId, "B upload");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/modules/{moduleId}/student-resources",
        studentA.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.Contains(resources, r => r.Id == ownId);
    Assert.DoesNotContain(resources, r => r.Id == otherId);
  }

  [Fact]
  public async Task GetModuleStudentResources_AsNonMember_ReturnsForbidden()
  {
    var teacher = await CreateTeacherAsync("resource.studentlist.foreign@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var outsider = await RegisterAsync("resource.studentlist.foreign.outsider@example.com");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/modules/{moduleId}/student-resources",
        outsider.AccessToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task GetUserResources_ExcludesStudentSubmissions()
  {
    var teacher = await CreateTeacherAsync("resource.mine.exclude@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    var moduleId = await CreateModuleAsync(teacher.AccessToken, courseId);

    var student = await RegisterAsync("resource.mine.exclude.student@example.com");
    await EnrollAsync(student.AccessToken, teacher.AccessToken, courseId, "resource.mine.exclude.student@example.com");
    var submittedId = await CreateStudentResourceAsync(student.AccessToken, moduleId, "Hidden upload");

    var response = await SendAuthorizedAsync(
        HttpMethod.Get,
        "/api/resources/mine",
        student.AccessToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var resources = await response.Content.ReadFromJsonAsync<List<ResourceDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(resources);
    Assert.DoesNotContain(resources, r => r.Id == submittedId);
  }
}
