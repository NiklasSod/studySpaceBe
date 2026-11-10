using System.Net;
using System.Net.Http.Json;
using lmsPortalBe.DTOs.Admin;
using lmsPortalBe.DTOs.Auth;
using lmsPortalBe.DTOs.Course;
using lmsPortalBe.DTOs.Friend;
using lmsPortalBe.DTOs.User;
using lmsPortalBe.DTOs.UserProfile;

namespace lmsPortalBe.Tests;

public class FriendsControllerTests : ApiTestBase, IClassFixture<TestWebApplicationFactory>
{
  public FriendsControllerTests(TestWebApplicationFactory factory) : base(factory)
  {
  }

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
          Name = $"Friend test {Guid.NewGuid():N}",
          Description = "Friend test course",
          StartDate = new DateTime(2027, 1, 1),
          EndDate = new DateTime(2027, 1, 31)
        });
    response.EnsureSuccessStatusCode();

    var course = await response.Content.ReadFromJsonAsync<CourseSummaryDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(course);
    return course.Id;
  }

  [Fact]
  public async Task SharedCourseMembersSeeMaskedEmailsUntilFriendRequestIsAccepted()
  {
    var requester = await RegisterAsync("friends.requester@example.com");
    var otherStudent = await RegisterAsync("friends.other@example.com");
    var outsider = await RegisterAsync("friends.outsider@example.com");
    var teacher = await CreateTeacherAsync("friends.teacher@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);

    await EnrollAndApproveAsync(requester.AccessToken, teacher.AccessToken, courseId, "friends.requester@example.com");
    await EnrollAndApproveAsync(otherStudent.AccessToken, teacher.AccessToken, courseId, "friends.other@example.com");

    var requesterId = await GetUserIdAsync("friends.requester@example.com");
    var otherStudentId = await GetUserIdAsync("friends.other@example.com");
    var outsiderId = await GetUserIdAsync("friends.outsider@example.com");
    var teacherId = await GetUserIdAsync("friends.teacher@example.com");

    var membersResponse = await SendAuthorizedAsync(HttpMethod.Get, "/api/users", requester.AccessToken);
    var members = await membersResponse.Content.ReadFromJsonAsync<List<UserDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(members);
    Assert.Contains(members, user => user.Id == otherStudentId
        && user.Email == "f*****@example.com"
        && !user.IsFriend);
    Assert.Contains(members, user => user.Id == teacherId
        && user.Email == "f*****@example.com"
        && user.Role == "teacher");
    Assert.DoesNotContain(members, user => user.Id == outsiderId);

    var courseResponse = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/courses/{courseId}",
        requester.AccessToken);
    var course = await courseResponse.Content.ReadFromJsonAsync<CourseDetailDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(course);
    Assert.Contains(course.Enrollments, member => member.UserId == teacherId
        && member.Email == "f*****@example.com"
        && !member.IsFriend);

    var teacherProfile = await SendAuthorizedAsync(
        HttpMethod.Post,
        "/api/profiles",
        teacher.AccessToken,
        new CreateUserProfileRequestDto { AboutMe = "Teacher profile" });
    teacherProfile.EnsureSuccessStatusCode();

    var sendResponse = await SendAuthorizedAsync(
        HttpMethod.Post,
        $"/api/friends/requests/{teacherId}",
        requester.AccessToken,
        new CreateFriendRequestDto { Message = "Would you like to connect?" });
    Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);

    var request = await sendResponse.Content.ReadFromJsonAsync<FriendRequestDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(request);
    Assert.Equal(requesterId, request.SenderUserId);
    Assert.Equal(teacherId, request.RecipientUserId);
    Assert.Equal("Would you like to connect?", request.Message);

    var blockedProfile = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/profiles/{teacherId}",
        requester.AccessToken);
    Assert.Equal(HttpStatusCode.NotFound, blockedProfile.StatusCode);

    var receivedResponse = await SendAuthorizedAsync(
        HttpMethod.Get,
        "/api/friends/requests/received",
        teacher.AccessToken);
    var receivedRequests = await receivedResponse.Content
        .ReadFromJsonAsync<List<FriendRequestDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(receivedRequests);
    Assert.Contains(receivedRequests, item => item.Id == request.Id);

    var acceptResponse = await SendAuthorizedAsync(
        HttpMethod.Post,
        $"/api/friends/requests/{request.Id}/accept",
        teacher.AccessToken);
    Assert.Equal(HttpStatusCode.NoContent, acceptResponse.StatusCode);

    var visibleProfile = await SendAuthorizedAsync(
        HttpMethod.Get,
        $"/api/profiles/{teacherId}",
        requester.AccessToken);
    Assert.Equal(HttpStatusCode.OK, visibleProfile.StatusCode);

    var friendsResponse = await SendAuthorizedAsync(HttpMethod.Get, "/api/friends", requester.AccessToken);
    var friends = await friendsResponse.Content.ReadFromJsonAsync<List<FriendDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(friends);
    Assert.Contains(friends, friend => friend.UserId == teacherId
        && friend.Email == "friends.teacher@example.com");

    var updatedMembersResponse = await SendAuthorizedAsync(HttpMethod.Get, "/api/users", requester.AccessToken);
    var updatedMembers = await updatedMembersResponse.Content
        .ReadFromJsonAsync<List<UserDto>>(TestContext.Current.CancellationToken);
    Assert.NotNull(updatedMembers);
    Assert.Contains(updatedMembers, user => user.Id == teacherId
        && user.Email == "friends.teacher@example.com"
        && user.IsFriend);

    var unrelatedRequest = await SendAuthorizedAsync(
        HttpMethod.Post,
        $"/api/friends/requests/{teacherId}",
        outsider.AccessToken,
        new CreateFriendRequestDto { Message = "Not in this course" });
    Assert.Equal(HttpStatusCode.NotFound, unrelatedRequest.StatusCode);
  }

  [Fact]
  public async Task DeclinedRequestCanBeResubmittedAndMessageIsLimitedTo140Characters()
  {
    var requester = await RegisterAsync("friends.decline.requester@example.com");
    var teacher = await CreateTeacherAsync("friends.decline.teacher@example.com");
    var courseId = await CreateCourseAsync(teacher.AccessToken);
    await EnrollAndApproveAsync(
        requester.AccessToken,
        teacher.AccessToken,
        courseId,
        "friends.decline.requester@example.com");
    var teacherId = await GetUserIdAsync("friends.decline.teacher@example.com");

    var invalidRequest = await SendAuthorizedAsync(
        HttpMethod.Post,
        $"/api/friends/requests/{teacherId}",
        requester.AccessToken,
        new CreateFriendRequestDto { Message = new string('x', 141) });
    Assert.Equal(HttpStatusCode.BadRequest, invalidRequest.StatusCode);

    var sendResponse = await SendAuthorizedAsync(
        HttpMethod.Post,
        $"/api/friends/requests/{teacherId}",
        requester.AccessToken,
        new CreateFriendRequestDto { Message = new string('x', 140) });
    Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
    var request = await sendResponse.Content.ReadFromJsonAsync<FriendRequestDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(request);

    var declineResponse = await SendAuthorizedAsync(
        HttpMethod.Post,
        $"/api/friends/requests/{request.Id}/decline",
        teacher.AccessToken);
    Assert.Equal(HttpStatusCode.NoContent, declineResponse.StatusCode);

    var resubmission = await SendAuthorizedAsync(
        HttpMethod.Post,
        $"/api/friends/requests/{teacherId}",
        requester.AccessToken,
        new CreateFriendRequestDto { Message = "Checking again" });
    Assert.Equal(HttpStatusCode.Created, resubmission.StatusCode);
  }
}
