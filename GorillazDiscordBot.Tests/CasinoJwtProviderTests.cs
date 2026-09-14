using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using GorillazDiscordBot.Services;
using Microsoft.IdentityModel.Tokens;

namespace GorillazDiscordBot.Tests;

public class CasinoJwtProviderTests
{
    private const string SigningKey = "dev-only-signing-key-please-rotate-before-production-0123456789ab";
    private const string Issuer = "LuckyMonkey";
    private const string Audience = "LuckyMonkey.Clients";

    public CasinoJwtProviderTests()
    {
        Environment.SetEnvironmentVariable(CasinoJwtProvider.SigningKeyEnv, SigningKey);
        Environment.SetEnvironmentVariable(CasinoJwtProvider.IssuerEnv, Issuer);
        Environment.SetEnvironmentVariable(CasinoJwtProvider.AudienceEnv, Audience);
    }

    [Fact]
    public void BearerFor_GeraTokenComIdentidadeDoUsuario()
    {
        var fixedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var bearer = CasinoJwtProvider.BearerFor(12345678901234, fixedDate);

        bearer.Should().StartWith("Bearer ");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(bearer["Bearer ".Length..]);
        token.Subject.Should().Be("12345678901234");
        token.Issuer.Should().Be(Issuer);
        token.Audiences.Should().Contain(Audience);
        token.ValidFrom.Should().BeCloseTo(fixedDate.UtcDateTime, TimeSpan.FromSeconds(1));
        token.ValidTo.Should().BeCloseTo(fixedDate.AddMinutes(10).UtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void BearerFor_AssinaturaAceitaComChaveDoServico()
    {
        var validation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(
            CasinoJwtProvider.BearerFor(777)[7..],
            validation,
            out _);

        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("777");
    }

    [Fact]
    public void BearerFor_RejeitaChaveDeAssinaturaDiferente()
    {
        var validation = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("outra-chave-de-assinatura-bem-longa-0123456789")),
            ValidateLifetime = true
        };

        var act = () => new JwtSecurityTokenHandler().ValidateToken(
            CasinoJwtProvider.BearerFor(777)[7..], validation, out _);

        act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }
}