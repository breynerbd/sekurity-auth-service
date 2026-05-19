using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain.Entitis;

public class UserEmail
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public bool EmailVerified { get; set; } = false;

    [MaxLength(255)]
    public string? EmailVerificationToken { get; set; }

    public DateTime? EmailVerificationTokenExpiry { get; set; }

    // Relación con User
    public User User { get; set; } = null!;
}