using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace APIUsuarios.Controllers
{
    public record TokenValidationRequest(string? token);

    public record TokenIntrospectionResponse(
        bool valid,
        string? reason,
        IDictionary<string, object>? claims);

    [ApiController]
    [Route("auth")]
    public class AuthValidationController : ControllerBase
    {
        private readonly IConfiguration _cfg;

        public AuthValidationController(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        /// <summary>
        /// Introspecção/validação de JWT.
        /// Em produção, gateways e outras APIs devem validar localmente (sem chamada remota).
        /// </summary>
        [HttpPost("validate")]
        [AllowAnonymous]
        public IActionResult ValidateToken([FromBody] TokenValidationRequest body)
        {
            if (body is null || string.IsNullOrWhiteSpace(body.token))
                return BadRequest(new { error = "Token ausente." });

            // 🔴 ESTA PARTE AQUI ESTÁ ERRADA:
            // var key = _cfg["Jwt:Key"] ?? "dev-secret-change-me";
            // var validationParameters = new TokenValidationParameters
            // {
            //     ValidateIssuer = true,
            //     ValidIssuer = issuer,
            //     ValidateAudience = true,
            //     ValidAudience = audience,
            //     ValidateIssuerSigningKey = true,
            //     IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            //     ValidateLifetime = true,
            //     ClockSkew = TimeSpan.FromSeconds(30)
            // };

            // ✅ SUBSTITUA PELO TRECHO CORRIGIDO ABAIXO:
            string rawKey = _cfg["Jwt:Key"];
            byte[] keyBytes = rawKey.StartsWith("base64:")
                ? Convert.FromBase64String(rawKey["base64:".Length..])
                : Encoding.UTF8.GetBytes(rawKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _cfg["Jwt:Issuer"] ?? "GamesPlatform",
                ValidateAudience = true,
                ValidAudience = _cfg["Jwt:Audience"] ?? "games-platform",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new JwtSecurityTokenHandler();

            try
            {
                handler.ValidateToken(body.token, validationParameters, out var validatedToken);
                var jwt = validatedToken as JwtSecurityToken ?? handler.ReadJwtToken(body.token);

                var claimsDict = jwt.Claims
                    .GroupBy(c => c.Type)
                    .ToDictionary(g => g.Key, g =>
                    {
                        var values = g.Select(c => c.Value).ToList();
                        return values.Count == 1 ? (object)values[0] : values;
                    });

                claimsDict["alg"] = jwt.Header.Alg;
                claimsDict["typ"] = jwt.Header.Typ;

                return Ok(new TokenIntrospectionResponse(
                    valid: true,
                    reason: null,
                    claims: claimsDict
                ));
            }
            catch (SecurityTokenExpiredException ex)
            {
                return Ok(new TokenIntrospectionResponse(false, $"expired: {ex.Message}", null));
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                return Ok(new TokenIntrospectionResponse(false, $"invalid_signature: {ex.Message}", null));
            }
            catch (SecurityTokenValidationException ex)
            {
                return Ok(new TokenIntrospectionResponse(false, $"invalid_token: {ex.Message}", null));
            }
            catch (Exception ex)
            {
                return Ok(new TokenIntrospectionResponse(false, $"error: {ex.Message}", null));
            }
        }


    }
}
