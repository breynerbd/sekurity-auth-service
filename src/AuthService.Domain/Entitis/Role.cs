using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain.Entitis;

public class Role
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string Description { get; set; } = null!;

    // Relación con UserRole
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}