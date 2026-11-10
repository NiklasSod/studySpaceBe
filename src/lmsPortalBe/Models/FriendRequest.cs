using System.ComponentModel.DataAnnotations;

namespace lmsPortalBe.Models;

public class FriendRequest
{
  public int Id { get; set; }

  [MaxLength(450)]
  public string SenderUserId { get; set; } = string.Empty;

  [MaxLength(450)]
  public string RecipientUserId { get; set; } = string.Empty;

  [MaxLength(450)]
  public string UserOneId { get; set; } = string.Empty;

  [MaxLength(450)]
  public string UserTwoId { get; set; } = string.Empty;

  [MaxLength(140)]
  public string? Message { get; set; }

  public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public DateTime? RespondedAt { get; set; }

  public ApplicationUser Sender { get; set; } = null!;

  public ApplicationUser Recipient { get; set; } = null!;
}
