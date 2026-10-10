using System.ComponentModel.DataAnnotations;

namespace lmsPortalBe.DTOs.Resource
{
    public class CreateResourceRequestDto
    {
        [Required]
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<string>? AudioUrls { get; set; }
        [Required]
        public string Url { get; set; } = string.Empty;
        public bool IsInteractiveImage { get; set; }
        public List<ImagePointDto>? Points { get; set; }
        public int? CourseId { get; set; }
        public int? ActivityId { get; set; }
        public int? ModuleId { get; set; }
    }
}