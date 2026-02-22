using System.ComponentModel.DataAnnotations;
using BikePartsTracker.Models;

namespace BikePartsTracker.DTOs
{
    public class PartDto
    {
        public Guid Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public PartType PartType { get; set; }

        [Required]
        public string Brand { get; set; } = string.Empty;

        [Required]
        public string Model { get; set; } = string.Empty;

        [Required]
        public DateTime InstallationDate { get; set; }

        [Required]
        public double MileageAtInstallation { get; set; }

        [Required]
        public Guid BikeId { get; set; }
    }

    public class CreatePartDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public PartType PartType { get; set; }

        public string? Description { get; set; }

        public string? Brand { get; set; }

        public string? Model { get; set; }

        public DateTime? InstallationDate { get; set; }

        [Required]
        public double MileageAtInstallation { get; set; }

        public bool? IsActive { get; set; }

        public Guid? BikeId { get; set; }
    }
}

