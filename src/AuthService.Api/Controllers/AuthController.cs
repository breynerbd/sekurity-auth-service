using BCrypt.Net;
using AuthService.Application.DTOs.Auth;
using AuthService.Application.Services;
using AuthService.Domain.Entitis;
using AuthService.Domain.Interfaces;
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

    /// <summary>
    /// Registra un nuevo usuario.
    /// </summary>
    /// <remarks>
    /// Este endpoint permite registrar un usuario enviando los datos mediante multipart/form-data.
    /// Soporta carga de imagen de perfil.
    /// </remarks>
    /// <param name="registerDto">Datos del usuario a registrar.</param>
    /// <response code="201">Usuario registrado exitosamente.</response>
    /// <response code="400">Datos inválidos.</response>
    /// <response code="409">El usuario ya existe.</response>
    [HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequest request)
{
    if (await _userRepository.ExistsByEmailAsync(request.Email))
        return BadRequest("Email already exists");


    var existingUser = await _userRepository.GetByUsernameAsync(request.Username);
if (existingUser != null)
{
    return BadRequest(new { Message = "El nombre de usuario ya está en uso." });
}
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

    var userRole = await _roleRepository.GetByNameAsync("USER");

    await _userRepository.UpdateUserRoleAsync(user.Id, userRole.Id);

    return Ok("User registered successfully");
}

    /// <summary>
    /// Inicia sesión en el sistema.
    /// </summary>
    /// <remarks>
    /// Permite autenticar a un usuario con sus credenciales.
    /// </remarks>
    /// <param name="loginDto">Credenciales del usuario.</param>
    /// <response code="200">Login exitoso.</response>
    /// <response code="401">Credenciales inválidas.</response>
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

/// <summary>
    /// Obtiene el perfil del usuario autenticado.
    /// </summary>
    /// <remarks>
    /// Requiere un token JWT válido en el header Authorization.
    /// </remarks>
    /// <response code="200">Perfil obtenido exitosamente.</response>
    /// <response code="401">No autorizado.</response>
    /// <response code="404">Usuario no encontrado.</response>
    [Authorize]
[HttpGet("me")]
public async Task<IActionResult> Me()
{
    // Obtenemos el Id del usuario desde el token
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Unauthorized();

    // Traemos al usuario con todas sus relaciones necesarias
    var user = await _userRepository.GetByIdAsync(userId);

    if (user == null)
        return NotFound("Usuario no encontrado.");

    // Construimos la respuesta
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

    /// <summary>
    /// Solicita recuperación de contraseña.
    /// </summary>
    /// <remarks>
    /// Siempre devuelve éxito por seguridad, incluso si el usuario no existe.
    /// </remarks>
    /// <param name="forgotPasswordDto">Correo del usuario.</param>
    /// <response code="200">Correo enviado (si aplica).</response>
    /// <response code="503">Error al enviar el correo.</response>
[HttpPost("forgot-password")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);

        if (!result.Success)
        {
            return StatusCode(503, result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Restablece la contraseña del usuario.
    /// </summary>
    /// <param name="resetPasswordDto">Token y nueva contraseña.</param>
    /// <response code="200">Contraseña actualizada correctamente.</response>
    /// <response code="400">Token inválido o expirado.</response>
    [HttpPost("reset-password")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<EmailResponseDto>> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        var result = await _authService.ResetPasswordAsync(resetPasswordDto);
        return Ok(result);
    }

    /// <summary>
    /// Reenvía el correo de verificación.
    /// </summary>
    /// <param name="resendDto">Correo del usuario.</param>
    /// <response code="200">Correo reenviado exitosamente.</response>
    /// <response code="400">El correo ya está verificado.</response>
    /// <response code="404">Usuario no encontrado.</response>
    /// <response code="503">Error al enviar el correo.</response>
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