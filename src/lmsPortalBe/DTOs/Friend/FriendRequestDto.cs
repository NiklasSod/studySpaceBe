namespace lmsPortalBe.DTOs.Friend;

public class FriendRequestDto
{
  public int Id { get; set; }

  public string SenderUserId { get; set; } = string.Empty;

  public string SenderFirstName { get; set; } = string.Empty;

  public string SenderLastName { get; set; } = string.Empty;

  public string RecipientUserId { get; set; } = string.Empty;

  public string RecipientFirstName { get; set; } = string.Empty;

  public string RecipientLastName { get; set; } = string.Empty;

  public string? Message { get; set; }

  public string Status { get; set; } = string.Empty;

  public DateTimeOffset CreatedAt { get; set; }
}
