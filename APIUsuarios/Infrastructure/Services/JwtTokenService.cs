using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using APIUsuarios.Domain;
using Microsoft.IdentityModel.Tokens;

namespace APIUsuarios.Infrastructure.Services
{
    public class JwtTokenService
    {
        private readonly byte[] _key;
        private readonly JwtOptions _opt;

        public JwtTokenService(byte[] keyBytes, JwtOptions opt)
        {
            _key = keyBytes;
            _opt = opt;
        }

        // validade padrão: 30 minutos (ajuste se quiser)
        public string Create(User u, int minutes = 30)
        {
            var creds = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("sub",   u.Id.ToString()),
                new Claim("email", u.Email),
                new Claim("role",  u.Role.ToString()) // "ADMIN"/"ALUNO"
            };

            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: _opt.Issuer,
                audience: _opt.Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(minutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
