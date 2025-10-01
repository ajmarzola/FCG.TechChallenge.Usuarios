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
        [AllowAnonymous] // é normal deixar público, pois apenas valida o token recebido
        public IActionResult ValidateToken([FromBody] TokenValidationRequest body)
        {
            if (body is null || string.IsNullOrWhiteSpace(body.token))
                return BadRequest(new { error = "Token ausente." });

            var issuer = _cfg["Jwt:Issuer"] ?? "GamesPlatform";
            var audience = _cfg["Jwt:Audience"] ?? "games-platform";
            var key = _cfg["Jwt:Key"] ?? "dev-secret-change-me";

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30) // tolerância de relógio
            };

            var handler = new JwtSecurityTokenHandler();

            try
            {
                // Valida assinatura, issuer, audience e expiração
                handler.ValidateToken(body.token, validationParameters, out var validatedToken);

                // Extrai claims do token (sem re-assinar nada)
                var jwt = validatedToken as JwtSecurityToken
                          ?? handler.ReadJwtToken(body.token);

                var claimsDict = jwt.Claims
                    .GroupBy(c => c.Type)
                    .ToDictionary(g => g.Key, g =>
                    {
                        // Se houver múltiplas claims com o mesmo tipo, retorna como lista
                        var values = g.Select(c => c.Value).ToList();
                        return values.Count == 1 ? (object)values[0] : values;
                    });

                // Também inclui cabeçalhos úteis (iss, aud, exp, iat já vêm nas claims na maioria dos casos)
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
                return Ok(new TokenIntrospectionResponse(
                    valid: false,
                    reason: $"expired: {ex.Message}",
                    claims: null
                ));
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                return Ok(new TokenIntrospectionResponse(
                    valid: false,
                    reason: $"invalid_signature: {ex.Message}",
                    claims: null
                ));
            }
            catch (SecurityTokenValidationException ex)
            {
                return Ok(new TokenIntrospectionResponse(
                    valid: false,
                    reason: $"invalid_token: {ex.Message}",
                    claims: null
                ));
            }
            catch (Exception ex)
            {
                return Ok(new TokenIntrospectionResponse(
                    valid: false,
                    reason: $"error: {ex.Message}",
                    claims: null
                ));
            }
        }
    }
}
