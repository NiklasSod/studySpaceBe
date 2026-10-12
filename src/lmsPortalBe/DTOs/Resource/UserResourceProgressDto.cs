namespace lmsPortalBe.DTOs.Resource
{
    public class UserResourceProgressDto
    {
        public int ResourceId { get; set; }
        public string UserProgressStatus { get; set; } = "Current";
        public bool IsFavorite { get; set; }
    }
}
