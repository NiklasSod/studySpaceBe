namespace lmsPortalBe.Models
{
    /// <summary>
    /// A clickable hotspot on an interactive image. X and Y are normalized
    /// (0.0 to 1.0) relative to the rendered image's width and height, so the
    /// position stays correct regardless of the size the image is displayed at.
    /// </summary>
    public class ImagePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Text { get; set; } = string.Empty;
        public string AudioUrl { get; set; } = string.Empty;
    }
}
