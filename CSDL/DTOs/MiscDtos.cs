namespace Nhom1.DTOs
{
    public class AmenitiesListDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RelatedUrl { get; set; }
    }

    public class MarkNotificationReadDto
    {
        public List<int> NotificationIds { get; set; } = new();
    }
}
