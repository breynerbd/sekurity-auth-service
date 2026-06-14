using BCrypt.Net;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Services;
using AuthService.Domain.Entitis;
using AuthService.Domain.Interfaces;
using AuthService.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AuthService.Application.DTOs;
using AuthService.Application.DTOs.Email;
using AuthService.Application.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IAuthService _authService;

    public AuthController(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IAuthService authService)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email))
            return BadRequest(new { Message = "El correo electrónico ya existe." });

        var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
        if (existingUser != null)
            return BadRequest(new { Message = "El nombre de usuario ya está en uso." });

        var user = new User
        {
            Id = Guid.NewGuid().ToString("N")[..16],
            Name = request.Name,
            Surname = request.Surname,
            Username = request.Username,
            Email = request.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Status = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);

        // ==========================================
        // ASIGNACIÓN Y VALIDACIÓN DE ROL DINÁMICO
        // ==========================================
        var roleName = string.IsNullOrWhiteSpace(request.Role)
            ? RoleConstants.USER_ROL
            : request.Role.ToUpper();

        if (!RoleConstants.AllowedRoles.Contains(roleName))
            return BadRequest(new { message = "Rol inválido." });

        if (roleName == RoleConstants.MASTER_ADMIN)
            return BadRequest(new { message = "No puedes asignar el rol MASTER_ADMIN mediante registro público." });

        var userRole = await _roleRepository.GetByNameAsync(roleName);
        if (userRole == null)
            return BadRequest(new { message = "El rol solicitado no existe en el sistema." });

        await _userRepository.UpdateUserRoleAsync(user.Id, userRole.Id);

        return Ok(new { Message = "Usuario registrado exitosamente con el rol: " + roleName });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized("Credenciales inválidas.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized("Credenciales inválidas.");

        var roles = await _userRepository.GetUserRolesAsync(user.Id);
        var token = _jwtTokenGenerator.GenerateToken(user, roles);

        return Ok(new { accessToken = token });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        // Se cambia a "sub" para alinearse al estándar del commit de kinRural
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound("Usuario no encontrado.");

        var result = new
        {
            user.Id,
            user.Name,
            user.Surname,
            user.Username,
            user.Email,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            EmailVerified = user.UserEmail?.EmailVerified ?? false,
            Profile = user.UserProfile != null ? new
            {
                user.UserProfile.ProfilePictureUrl,
                user.UserProfile.Bio,
                user.UserProfile.DateOfBirth
            } : null,
            Roles = user.UserRoles.Select(r => r.Role.Name).ToList()
        };

        return Ok(result);
    }

    // =========================================================================
    // NUEVO ENDPOINT JERÁRQUICO DE ROLES (Agregado del commit de server-admin)
    // =========================================================================
    [Authorize(Roles = "MASTER_ADMIN,ADMIN")]
    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateUserRole(string id, [FromBody] UpdateRoleRequest request)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null)
            return NotFound(new { message = "Usuario no encontrado." });

        var currentUserRole = User.FindFirst(ClaimTypes.Role)?.Value;

        var targetRoles = user.UserRoles.Select(r => r.Role.Name).ToList();
        var targetIsMasterAdmin = targetRoles.Contains(RoleConstants.MASTER_ADMIN);
        var targetIsAdmin = targetRoles.Contains(RoleConstants.ADMIN_ROL);

        // Restricciones para los administradores estándar
        if (currentUserRole == RoleConstants.ADMIN_ROL)
        {
            if (targetIsAdmin || targetIsMasterAdmin)
            {
                return StatusCode(403, new { message = "No tienes permisos para modificar administradores o cuentas raíz." });
            }
        }

        // Restricciones para el Master Admin (evitar auto-eliminación o guerras de privilegios)
        if (currentUserRole == RoleConstants.MASTER_ADMIN)
        {
            if (targetIsMasterAdmin)
            {
                return StatusCode(403, new { message = "No puedes modificar a otro usuario MASTER_ADMIN por seguridad de la plataforma." });
            }
        }

        var role = await _roleRepository.GetByNameAsync(request.Role.ToUpper());
        if (role == null)
            return NotFound(new { message = "El rol que intentas asignar no existe." });

        await _userRepository.UpdateUserRoleAsync(id, role.Id);

        return Ok(new { message = "Rol actualizado correctamente a: " + request.Role.ToUpper() });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);
        if (!result.Success) return StatusCode(503, result);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        var result = await _authService.ResetPasswordAsync(resetPasswordDto);
        return Ok(result);
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ResendVerification([FromBody] ResendVerificationDto resendDto)
    {
        var result = await _authService.ResendVerificationEmailAsync(resendDto);

        if (!result.Success)
        {
            if (result.Message.Contains("no encontrado", StringComparison.OrdinalIgnoreCase))
                return NotFound(result);

            if (result.Message.Contains("ya ha sido verificado", StringComparison.OrdinalIgnoreCase))
                return BadRequest(result);

            return StatusCode(503, result);
        }

        return Ok(result);
    }
}