using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        public string StravaId { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiry { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Bike> Bikes { get; set; } = new List<Bike>();
    }
}