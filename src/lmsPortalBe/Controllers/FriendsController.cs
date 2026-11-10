using System.Security.Claims;
using lmsPortalBe.Data;
using lmsPortalBe.DTOs.Friend;
using lmsPortalBe.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace lmsPortalBe.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController(ILmsPortalContext context) : ControllerBase
{
  private readonly ILmsPortalContext _context = context;

  private string CurrentUserId =>
      User.FindFirstValue(ClaimTypes.NameIdentifier)
      ?? throw new UnauthorizedAccessException("User identity not found.");

  [HttpGet]
  public async Task<IActionResult> GetFriends()
  {
    var currentUserId = CurrentUserId;
    var friendships = await _context.FriendRequests
        .Include(r => r.Sender)
        .Include(r => r.Recipient)
        .Where(r => r.Status == FriendRequestStatus.Accepted
            && (r.SenderUserId == currentUserId || r.RecipientUserId == currentUserId))
        .ToListAsync();

    var friends = friendships.Select(r => ToFriendDto(
        r.SenderUserId == currentUserId ? r.Recipient : r.Sender));

    return Ok(friends.OrderBy(f => f.FirstName).ThenBy(f => f.LastName));
  }

  [HttpGet("requests/received")]
  public async Task<IActionResult> GetReceivedRequests()
  {
    var requests = await _context.FriendRequests
        .Include(r => r.Sender)
        .Include(r => r.Recipient)
        .Where(r => r.RecipientUserId == CurrentUserId
            && r.Status == FriendRequestStatus.Pending)
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync();

    return Ok(requests.Select(ToRequestDto));
  }

  [HttpGet("requests/sent")]
  public async Task<IActionResult> GetSentRequests()
  {
    var requests = await _context.FriendRequests
        .Include(r => r.Sender)
        .Include(r => r.Recipient)
        .Where(r => r.SenderUserId == CurrentUserId
            && r.Status == FriendRequestStatus.Pending)
        .OrderByDescending(r => r.CreatedAt)
        .ToListAsync();

    return Ok(requests.Select(ToRequestDto));
  }

  [HttpGet("requests/{id:int}")]
  public async Task<IActionResult> GetRequest(int id)
  {
    var currentUserId = CurrentUserId;
    var request = await _context.FriendRequests
        .Include(r => r.Sender)
        .Include(r => r.Recipient)
        .FirstOrDefaultAsync(r => r.Id == id
            && (r.SenderUserId == currentUserId || r.RecipientUserId == currentUserId));

    return request is null ? NotFound() : Ok(ToRequestDto(request));
  }

  [HttpPost("requests/{userId}")]
  public async Task<IActionResult> SendRequest(string userId, CreateFriendRequestDto dto)
  {
    var currentUserId = CurrentUserId;
    if (string.Equals(currentUserId, userId, StringComparison.Ordinal))
    {
      return BadRequest("You cannot send a friend request to yourself.");
    }

    var sharedCourseIds = await _context.CourseEnrollments
        .Where(e => e.UserId == currentUserId
            && e.Status == CourseEnrollmentStatus.Approved
            && (e.Role == CourseRole.Student || e.Role == CourseRole.Teacher))
        .Select(e => e.CourseId)
        .ToListAsync();

    var sharesCourse = sharedCourseIds.Count > 0
        && await _context.CourseEnrollments.AnyAsync(e => e.UserId == userId
            && sharedCourseIds.Contains(e.CourseId)
            && e.Status == CourseEnrollmentStatus.Approved
            && (e.Role == CourseRole.Student || e.Role == CourseRole.Teacher));

    if (!sharesCourse)
    {
      return NotFound("Friend requests can only be sent to approved members of your courses.");
    }

    var recipient = await FindUserAsync(userId)
        ?? throw new InvalidOperationException("Approved course member has no associated user.");

    var userOneId = string.CompareOrdinal(currentUserId, userId) < 0 ? currentUserId : userId;
    var userTwoId = string.CompareOrdinal(currentUserId, userId) < 0 ? userId : currentUserId;
    var request = await _context.FriendRequests
        .FirstOrDefaultAsync(r => r.UserOneId == userOneId && r.UserTwoId == userTwoId);

    if (request is { Status: FriendRequestStatus.Accepted })
    {
      return Conflict("You are already friends.");
    }

    if (request is { Status: FriendRequestStatus.Pending })
    {
      return Conflict(request.SenderUserId == currentUserId
          ? "Your friend request is already pending."
          : "This user has already sent you a friend request.");
    }

    if (request is null)
    {
      request = new FriendRequest
      {
        SenderUserId = currentUserId,
        RecipientUserId = userId,
        UserOneId = userOneId,
        UserTwoId = userTwoId,
        Message = dto.Message,
        Status = FriendRequestStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        Sender = await FindUserAsync(currentUserId) ?? throw new UnauthorizedAccessException("User identity not found."),
        Recipient = recipient
      };
      _context.FriendRequests.Add(request);
    }
    else
    {
      request.SenderUserId = currentUserId;
      request.RecipientUserId = userId;
      request.Message = dto.Message;
      request.Status = FriendRequestStatus.Pending;
      request.CreatedAt = DateTime.UtcNow;
      request.RespondedAt = null;
      request.Sender = await FindUserAsync(currentUserId) ?? throw new UnauthorizedAccessException("User identity not found.");
      request.Recipient = recipient;
    }

    try
    {
      await _context.SaveChangesAsync();
    }
    catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
    {
      return Conflict("A friend request or friendship already exists.");
    }

    return CreatedAtAction(nameof(GetRequest), new { id = request.Id }, ToRequestDto(request));
  }

  [HttpPost("requests/{id:int}/accept")]
  public Task<IActionResult> AcceptRequest(int id) => RespondToRequestAsync(id, accept: true);

  [HttpPost("requests/{id:int}/decline")]
  public Task<IActionResult> DeclineRequest(int id) => RespondToRequestAsync(id, accept: false);

  [HttpDelete("{userId}")]
  public async Task<IActionResult> RemoveFriend(string userId)
  {
    var currentUserId = CurrentUserId;
    var userOneId = string.CompareOrdinal(currentUserId, userId) < 0 ? currentUserId : userId;
    var userTwoId = string.CompareOrdinal(currentUserId, userId) < 0 ? userId : currentUserId;
    var friendship = await _context.FriendRequests
        .FirstOrDefaultAsync(r => r.UserOneId == userOneId
            && r.UserTwoId == userTwoId
            && r.Status == FriendRequestStatus.Accepted);

    if (friendship is null)
    {
      return NotFound("Friendship not found.");
    }

    _context.FriendRequests.Remove(friendship);
    await _context.SaveChangesAsync();
    return NoContent();
  }

  private async Task<IActionResult> RespondToRequestAsync(int id, bool accept)
  {
    var request = await _context.FriendRequests
        .FirstOrDefaultAsync(r => r.Id == id && r.RecipientUserId == CurrentUserId);

    if (request is null)
    {
      return NotFound("Friend request not found.");
    }

    if (request.Status != FriendRequestStatus.Pending)
    {
      return Conflict("This friend request has already been answered.");
    }

    request.Status = accept ? FriendRequestStatus.Accepted : FriendRequestStatus.Declined;
    request.RespondedAt = DateTime.UtcNow;
    await _context.SaveChangesAsync();
    return NoContent();
  }

  private async Task<ApplicationUser?> FindUserAsync(string userId) =>
      await _context.CourseEnrollments
          .Where(e => e.UserId == userId)
          .Select(e => e.User)
          .FirstOrDefaultAsync();

  private static FriendDto ToFriendDto(ApplicationUser user) => new()
  {
    UserId = user.Id,
    FirstName = user.FirstName,
    LastName = user.LastName,
    Email = user.Email ?? string.Empty
  };

  private static FriendRequestDto ToRequestDto(FriendRequest request) => new()
  {
    Id = request.Id,
    SenderUserId = request.SenderUserId,
    SenderFirstName = request.Sender.FirstName,
    SenderLastName = request.Sender.LastName,
    RecipientUserId = request.RecipientUserId,
    RecipientFirstName = request.Recipient.FirstName,
    RecipientLastName = request.Recipient.LastName,
    Message = request.Message,
    Status = request.Status.ToString(),
    CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(request.CreatedAt, DateTimeKind.Utc))
  };

  private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
      exception.InnerException is SqliteException { SqliteErrorCode: 19 };
}
