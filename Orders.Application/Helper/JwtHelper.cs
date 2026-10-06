using Microsoft.IdentityModel.Tokens;
using Orders.Application.Dtos;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Orders.Application.Helper
{
    public class JwtHelper
    {
        public static string GenerateAccessToken(MappingUser user, string jwtKey, string issuer, string audience, int expireMinutes = 60)
        {
            var claims = new List<Claim>
            {
                new Claim("UserId", user.UserId),
                new Claim("UserFullName", user.FullName),
                new Claim("Email", user.Email),
                new Claim("RoleName", user.RoleName),
                new Claim("RoleId", user.RoleId.ToString()),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
