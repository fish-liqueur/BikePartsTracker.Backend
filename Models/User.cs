using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Bike> Bikes { get; set; } = new List<Bike>();
        
        /// <summary>
        /// External service integrations (Strava, Komoot, etc.)
        /// </summary>
        public ICollection<ExternalServiceIntegration> ExternalServiceIntegrations { get; set; } = new List<ExternalServiceIntegration>();
    }
}