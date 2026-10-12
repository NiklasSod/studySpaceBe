using System.Net;
using System.Net.Http.Json;
using lmsPortalBe.DTOs.Auth;
using Microsoft.AspNetCore.WebUtilities;

namespace lmsPortalBe.Tests;

public class AuthControllerTests : ApiTestBase, IClassFixture<TestWebApplicationFactory>
{
  public AuthControllerTests(TestWebApplicationFactory factory) : base(factory)
  {
  }

  [Fact]
  public async Task Register_WithValidRequest_ReturnsAccessTokenAndSetsRefreshCookie()
  {
    var response = await Client.PostAsJsonAsync("/api/auth/register", RegisterRequest("register.ok@example.com"), TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    Assert.NotEqual(default, body.ExpiresAt);

    Assert.False(string.IsNullOrWhiteSpace(ReadRefreshTokenCookie(response)));
  }

  [Fact]
  public async Task Register_WithWeakPassword_ReturnsBadRequest()
  {
    var response = await Client.PostAsJsonAsync("/api/auth/register", RegisterRequest("register.weak@example.com", "weak"), TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
  {
    var request = RegisterRequest("register.dupe@example.com");

    var first = await Client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, first.StatusCode);

    var second = await Client.PostAsJsonAsync("/api/auth/register", request, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
  }

  [Fact]
  public async Task Login_WithValidCredentials_ReturnsAccessTokenAndSetsRefreshCookie()
  {
    await RegisterAsync("login.ok@example.com");

    var response = await Client.PostAsJsonAsync(
        "/api/auth/login",
        new LoginRequestDto { Email = "login.ok@example.com", Password = "Passw0rd1" },
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    Assert.NotEqual(default, body.ExpiresAt);

    Assert.False(string.IsNullOrWhiteSpace(ReadRefreshTokenCookie(response)));
  }

  [Fact]
  public async Task Login_WithWrongPassword_ReturnsUnauthorized()
  {
    await RegisterAsync("login.wrong@example.com");

    var response = await Client.PostAsJsonAsync(
        "/api/auth/login",
        new LoginRequestDto { Email = "login.wrong@example.com", Password = "WrongPass1" },
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Refresh_WithValidCookie_RotatesRefreshToken()
  {
    var tokens = await RegisterWithTokensAsync("refresh.ok@example.com");

    var response = await Client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>(TestContext.Current.CancellationToken);
    Assert.NotNull(body);
    Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

    var rotated = ReadRefreshTokenCookie(response);
    Assert.False(string.IsNullOrWhiteSpace(rotated));
    Assert.NotEqual(tokens.RefreshToken, rotated);

    // The client persists the rotated cookie, so a second refresh succeeds.
    var reuse = await Client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, reuse.StatusCode);
  }

  [Fact]
  public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
  {
    var response = await Client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task Logout_RevokesRefreshToken()
  {
    await RegisterAsync("logout.ok@example.com");

    var logout = await Client.PostAsync("/api/auth/logout", null, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

    var refresh = await Client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
  }

  [Fact]
  public async Task ForgotPassword_ForRegisteredEmail_SendsResetLink()
  {
    var email = "forgot.known@example.com";
    await RegisterAsync(email);

    var response = await Client.PostAsJsonAsync(
        "/api/auth/forgot-password",
        new ForgotPasswordRequestDto { Email = email },
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var sent = Assert.Single(Factory.EmailSender.Sent, mail => mail.To == email);
    Assert.Contains($"email={Uri.EscapeDataString(email)}", sent.ResetLink);
    Assert.False(string.IsNullOrWhiteSpace(ExtractResetToken(sent.ResetLink)));
  }

  [Fact]
  public async Task ForgotPassword_ForUnknownEmail_ReturnsSameResponseWithoutSending()
  {
    var sentBefore = Factory.EmailSender.Sent.Count;

    var response = await Client.PostAsJsonAsync(
        "/api/auth/forgot-password",
        new ForgotPasswordRequestDto { Email = "forgot.unknown@example.com" },
        TestContext.Current.CancellationToken);

    // The same response as a registered address so accounts cannot be enumerated.
    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(sentBefore, Factory.EmailSender.Sent.Count);
  }

  [Fact]
  public async Task ResetPassword_WithValidToken_ChangesPasswordAndRevokesSessions()
  {
    var email = "reset.valid@example.com";
    await RegisterWithTokensAsync(email, "OldPassw0rd");

    await Client.PostAsJsonAsync(
        "/api/auth/forgot-password",
        new ForgotPasswordRequestDto { Email = email },
        TestContext.Current.CancellationToken);

    var resetLink = Assert.Single(Factory.EmailSender.Sent, mail => mail.To == email).ResetLink;

    var response = await Client.PostAsJsonAsync(
        "/api/auth/reset-password",
        new ResetPasswordRequestDto
        {
          Email = email,
          Token = ExtractResetToken(resetLink),
          NewPassword = "NewPassw0rd"
        },
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    // Sessions established before the reset are revoked.
    var refresh = await Client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

    var oldPassword = await Client.PostAsJsonAsync(
        "/api/auth/login",
        new LoginRequestDto { Email = email, Password = "OldPassw0rd" },
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);

    var newPassword = await Client.PostAsJsonAsync(
        "/api/auth/login",
        new LoginRequestDto { Email = email, Password = "NewPassw0rd" },
        TestContext.Current.CancellationToken);
    Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);
  }

  [Fact]
  public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
  {
    var email = "reset.invalid@example.com";
    await RegisterAsync(email);

    var response = await Client.PostAsJsonAsync(
        "/api/auth/reset-password",
        new ResetPasswordRequestDto
        {
          Email = email,
          Token = "not-a-valid-token",
          NewPassword = "NewPassw0rd"
        },
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task ResetPassword_ForUnknownEmail_ReturnsBadRequest()
  {
    var response = await Client.PostAsJsonAsync(
        "/api/auth/reset-password",
        new ResetPasswordRequestDto
        {
          Email = "reset.unknown@example.com",
          Token = "whatever",
          NewPassword = "NewPassw0rd"
        },
        TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  private static string ExtractResetToken(string resetLink)
  {
    var query = QueryHelpers.ParseQuery(new Uri(resetLink).Query);
    return query.TryGetValue("token", out var token) ? token.ToString() : string.Empty;
  }
}
