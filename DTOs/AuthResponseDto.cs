namespace BikePartsTracker.DTOs
{
    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public string? Message { get; set; }
        public UserDto? User { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int DefaultChainCycleLength { get; set; }

        /// <summary>
        /// Default chain-cycle interval in metres (aligned with
        /// <c>UserSettings.DefaultChainCycleIntervalMetres</c>, ADR 0002 E2).
        /// </summary>
        public int DefaultChainCycleInterval { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}








