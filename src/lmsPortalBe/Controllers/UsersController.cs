using System.Security.Claims;
using lmsPortalBe.Data;
using lmsPortalBe.DTOs.User;
using lmsPortalBe.Models;
using lmsPortalBe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lmsPortalBe.Controllers
{
  [ApiController]
  [Route("api/users")]
  [Authorize]
  public class UsersController(
      UserManager<ApplicationUser> userManager,
      ILmsPortalContext context) : ControllerBase
  {
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ILmsPortalContext _context = context;

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
      var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? throw new UnauthorizedAccessException("User identity not found.");
      var isAdmin = User.IsInRole("admin");

      var usersQuery = _userManager.Users.AsQueryable();
      if (!isAdmin)
      {
        var courseIds = await _context.CourseEnrollments
            .Where(e => e.UserId == currentUserId
                && e.Status == CourseEnrollmentStatus.Approved)
            .Select(e => e.CourseId)
            .ToListAsync();

        var memberIds = await _context.CourseEnrollments
            .Where(e => courseIds.Contains(e.CourseId)
                && e.Status == CourseEnrollmentStatus.Approved
                && e.UserId != currentUserId)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync();
        usersQuery = usersQuery.Where(u => memberIds.Contains(u.Id));
      }

      var users = await usersQuery
          .OrderBy(u => u.LastName)
          .ThenBy(u => u.FirstName)
          .ToListAsync();

      var userIds = users.Select(u => u.Id).ToList();
      var friendIds = await _context.FriendRequests
          .Where(r => r.Status == FriendRequestStatus.Accepted
              && (r.SenderUserId == currentUserId || r.RecipientUserId == currentUserId))
          .Select(r => r.SenderUserId == currentUserId ? r.RecipientUserId : r.SenderUserId)
          .Where(id => userIds.Contains(id))
          .ToHashSetAsync();

      var result = new List<UserDto>(users.Count);
      foreach (var user in users)
      {
        var roles = await _userManager.GetRolesAsync(user);
        var isFriend = friendIds.Contains(user.Id);
        result.Add(ToDto(user, roles, isFriend, maskEmail: !isAdmin && !isFriend));
      }

      return Ok(result);
    }

    [HttpGet("students")]
    [Authorize(Roles = "teacher,admin")]
    public async Task<IActionResult> GetStudents()
    {
      var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? throw new UnauthorizedAccessException("User identity not found.");

      var query = _context.CourseEnrollments
          .Where(e => e.Role == CourseRole.Student
              && e.Status == CourseEnrollmentStatus.Approved);

      if (!User.IsInRole("admin"))
      {
        var taughtCourseIds = await _context.CourseEnrollments
            .Where(e => e.UserId == currentUserId
                && e.Role == CourseRole.Teacher
                && e.Status == CourseEnrollmentStatus.Approved)
            .Select(e => e.CourseId)
            .ToListAsync();

        query = query.Where(e => taughtCourseIds.Contains(e.CourseId));
      }

      var studentIds = await query
          .Select(e => e.UserId)
          .Distinct()
          .ToListAsync();

      var students = await _userManager.Users
          .Where(u => studentIds.Contains(u.Id))
          .OrderBy(u => u.LastName)
          .ThenBy(u => u.FirstName)
          .ToListAsync();

      return Ok(students.Select(u => ToDto(u, ["student"])));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
      var user = await _userManager.FindByIdAsync(id);
      if (user is null)
      {
        return NotFound("User not found.");
      }

      var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? throw new UnauthorizedAccessException("User identity not found.");
      var isFriend = await _context.FriendRequests.AnyAsync(r =>
          r.Status == FriendRequestStatus.Accepted
          && ((r.SenderUserId == currentUserId && r.RecipientUserId == id)
              || (r.SenderUserId == id && r.RecipientUserId == currentUserId)));

      var sharesCourse = id == currentUserId || await _context.CourseEnrollments
          .Where(e => e.UserId == currentUserId
              && e.Status == CourseEnrollmentStatus.Approved)
          .Select(e => e.CourseId)
          .AnyAsync(courseId => _context.CourseEnrollments.Any(other =>
              other.UserId == id
              && other.CourseId == courseId
              && other.Status == CourseEnrollmentStatus.Approved));

      var isAdmin = User.IsInRole("admin");
      if (!isAdmin && !isFriend && !sharesCourse)
      {
        return NotFound("User not found.");
      }

      var roles = await _userManager.GetRolesAsync(user);
      return Ok(ToDto(user, roles, isFriend, maskEmail: !isAdmin && !isFriend && id != currentUserId));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<IActionResult> UpdateUser(string id, UpdateUserRequestDto dto)
    {
      var user = await _userManager.FindByIdAsync(id);
      if (user is null)
      {
        return NotFound("User not found.");
      }

      var roles = await _userManager.GetRolesAsync(user);
      if (roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
      {
        return BadRequest("Cannot edit an administrator.");
      }

      var isStudentOrTeacher = roles.Contains("student", StringComparer.OrdinalIgnoreCase)
          || roles.Contains("teacher", StringComparer.OrdinalIgnoreCase);
      if (!isStudentOrTeacher)
      {
        return BadRequest("Only users with a student or teacher role can be edited.");
      }

      user.FirstName = dto.FirstName;
      user.LastName = dto.LastName;

      if (!string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
      {
        var emailResult = await _userManager.SetEmailAsync(user, dto.Email);
        if (!emailResult.Succeeded)
        {
          return BadRequest(emailResult.Errors.Select(e => e.Description));
        }

        var userNameResult = await _userManager.SetUserNameAsync(user, dto.Email);
        if (!userNameResult.Succeeded)
        {
          return BadRequest(userNameResult.Errors.Select(e => e.Description));
        }
      }
      else
      {
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
          return BadRequest(updateResult.Errors.Select(e => e.Description));
        }
      }

      return NoContent();
    }

    private static UserDto ToDto(
        ApplicationUser user,
        IList<string> roles,
        bool isFriend = false,
        bool maskEmail = false) => new()
    {
      Id = user.Id,
      FirstName = user.FirstName,
      LastName = user.LastName,
      Email = maskEmail ? EmailMasker.Mask(user.Email ?? string.Empty) : user.Email ?? string.Empty,
      Role = roles.FirstOrDefault() ?? string.Empty,
      IsFriend = isFriend
    };
  }
}
