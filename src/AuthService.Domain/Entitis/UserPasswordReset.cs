using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain.Entitis;

public class UserPasswordReset
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(36)]
    public string UserId { get; set; } = string.Empty;

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }

    // Relación con User
    public User User { get; set; } = null!;
}