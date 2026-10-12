namespace lmsPortalBe.Models
{
    public class UserResourceProgress
    {
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
        public int ResourceId { get; set; }
        public Resource Resource { get; set; } = null!;
        public UserProgressStatus UserProgressStatus { get; set; } = UserProgressStatus.Current;
        public bool IsFavorite { get; set; }
    }
}
