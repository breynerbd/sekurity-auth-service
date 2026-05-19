using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthService.Domain.Entitis;

public class UserProfile
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(36)]
    [ForeignKey(nameof(User))]
    public string UserId { get; set; } = string.Empty;

    public string? ProfilePictureUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime DateOfBirth { get; set; }

    // Relación 1-1
    public User User { get; set; } = null!;
}