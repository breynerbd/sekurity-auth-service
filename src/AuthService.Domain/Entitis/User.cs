using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain.Entitis;

public class User
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [MaxLength(25)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es obligatorio")]
    [MaxLength(16)]
    public string Surname { get; set; } = string.Empty;

    [Required]
    [MaxLength(25)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public bool Status { get; set; } = true;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // 🔥 RELACIONES
    public UserProfile? UserProfile { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public UserEmail? UserEmail { get; set; }
    public UserPasswordReset? UserPasswordReset { get; set; }
}