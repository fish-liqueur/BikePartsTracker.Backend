using System.ComponentModel.DataAnnotations;

namespace BikePartsTracker.DTOs
{
    /// <summary>
    /// Data transfer object for user registration requests
    /// </summary>
    public class RegisterDto
    {
        /// <summary>
        /// User's full name
        /// </summary>
        /// <example>John Doe</example>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// User's email address
        /// </summary>
        /// <example>john@example.com</example>
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's password (minimum 6 characters)
        /// </summary>
        /// <example>password123</example>
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Password confirmation (must match Password)
        /// </summary>
        /// <example>password123</example>
        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
