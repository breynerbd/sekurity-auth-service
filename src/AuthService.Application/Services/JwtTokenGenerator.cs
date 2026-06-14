using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthService.Domain.Entitis;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;

namespace AuthService.Application.Services
{
    // Ahora implementa la interfaz
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly IConfiguration _configuration;

        public JwtTokenGenerator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user, IList<string> roles)
        {
            // 1️⃣ Generar la clave simétrica a partir de la configuración
            // Se agrega ?? string.Empty para solucionar la advertencia CS8604 de la consola
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? string.Empty)
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            // 2️⃣ Definir los claims del token usando nombres cortos estándar para el Frontend
            var claims = new List<Claim>
            {
                new Claim("id", user.Id),                            // ✅ En vez de ClaimTypes.NameIdentifier
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("username", user.Username)                  // ✅ En vez de ClaimTypes.Name
            };

            // Agregar roles como claims con nombre corto
            foreach (var role in roles)
            {
                claims.Add(new Claim("role", role));                  // ✅ En vez de ClaimTypes.Role
            }

            // 3️⃣ Crear el token JWT
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),       // duración del token
                signingCredentials: credentials
            );

            // 4️⃣ Devolver el token como string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}