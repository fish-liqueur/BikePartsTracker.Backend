using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for user login requests
    /// </summary>
    public class LoginDto
    {
        /// <summary>
        /// User's email address
        /// </summary>
        /// <example>user@example.com</example>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's password
        /// </summary>
        /// <example>password123</example>
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
