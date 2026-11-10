using System.ComponentModel.DataAnnotations;

namespace lmsPortalBe.DTOs.Friend;

public class CreateFriendRequestDto
{
  [MaxLength(140)]
  public string? Message { get; set; }
}
