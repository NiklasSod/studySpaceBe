using System.ComponentModel.DataAnnotations;

namespace lmsPortalBe.DTOs.Resource
{
    public class ImagePointDto
    {
        [Range(0, 1)]
        public double X { get; set; }

        [Range(0, 1)]
        public double Y { get; set; }

        public string Text { get; set; } = string.Empty;

        public string AudioUrl { get; set; } = string.Empty;
    }
}
