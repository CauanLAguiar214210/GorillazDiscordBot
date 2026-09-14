using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace GorillazDiscordBot.Services;

/// <summary>
/// Emite tokens JWT HS256 assinados com a mesma chave compartilhada do microserviço de cassino
/// (LuckyMonkey). A identidade do usuário vai na claim <c>sub</c> — o serviço nunca confia no
/// userId do body/path. O bot é dono do dinheiro e atua em nome do usuário do Discord.
/// </summary>
public static class CasinoJwtProvider
{
    public const string IssuerEnv = "LUCKY_MONKEY_JWT_ISSUER";
    public const string AudienceEnv = "LUCKY_MONKEY_JWT_AUDIENCE";
    public const string SigningKeyEnv = "LUCKY_MONKEY_JWT_SIGNING_KEY";

    private const string DefaultIssuer = "LuckyMonkey";
    private const string DefaultAudience = "LuckyMonkey.Clients";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    private static readonly Lazy<SymmetricSecurityKey> Key = new(() =>
    {
        var signingKey = Environment.GetEnvironmentVariable(SigningKeyEnv);
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new InvalidOperationException(
                $"LUCKY_MONKEY_JWT_SIGNING_KEY não configurada — o serviço de cassino exige JWT Bearer. " +
                "Use a mesma chave configurada no LuckyMonkey (ex.: LuckyMonkey:Jwt:SigningKey).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    });

    /// <summary>Empacota o token para o header <c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    public static string BearerFor(ulong userId, DateTimeOffset? now = null)
        => $"Bearer {BuildToken(userId, now)}";

    private static string BuildToken(ulong userId, DateTimeOffset? now)
    {
        var issuedAt = now ?? DateTimeOffset.UtcNow;
        var credentials = new SigningCredentials(Key.Value, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            }),
            Issuer = Environment.GetEnvironmentVariable(IssuerEnv) is { Length: > 0 } issuer
                ? issuer
                : DefaultIssuer,
            Audience = Environment.GetEnvironmentVariable(AudienceEnv) is { Length: > 0 } audience
                ? audience
                : DefaultAudience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = issuedAt.Add(TokenLifetime).UtcDateTime,
            SigningCredentials = credentials
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }
}