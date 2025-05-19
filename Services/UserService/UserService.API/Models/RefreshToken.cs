using System;

namespace UserService.API.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string JwtId { get; set; } = string.Empty;
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }
        public DateTimeOffset IssuedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }

        // Navigation property
        public virtual ApplicationUser? User { get; set; }
    }
} 