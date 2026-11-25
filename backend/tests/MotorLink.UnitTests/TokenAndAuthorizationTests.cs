using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MotorLink.API.Authorization;
using MotorLink.Application.Abstractions;
using MotorLink.Application.Options;
using MotorLink.Domain.Entities;
using MotorLink.Infrastructure.Identity;

namespace MotorLink.UnitTests;

public class TokenServiceTests
{
    private static readonly JwtOptions Opt = new()
    {
        Issuer = "iss", Audience = "aud", Key = new string('k', 40), AccessTokenMinutes = 15, RefreshTokenDays = 7
    };
    private readonly FakeClock _clock = new(DateTimeOffset.UtcNow);
    private TokenService Create() => new(Options.Create(Opt), _clock);

    [Fact]
    public void Refresh_tokens_are_unique_and_long_enough()
    {
        var s = Create();
        var set = Enumerable.Range(0, 50).Select(_ => s.GenerateRefreshToken()).ToHashSet();
        Assert.Equal(50, set.Count);
        Assert.All(set, t => Assert.True(t.Length >= 80)); // 64 bytes en base64url
    }

    [Fact]
    public void Hash_is_deterministic_hex_sha256_and_differs_from_input()
    {
        var s = Create();
        var h = s.HashToken("abc");
        Assert.Equal(h, s.HashToken("abc"));
        Assert.Equal(64, h.Length);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", h);
    }

    [Fact]
    public async Task Access_token_expires_in_15_minutes_and_is_validated_for_issuer_audience_and_signature()
    {
        var s = Create();
        var user = new UserAccount(Guid.NewGuid(), "a@b.com", "Ana", true, ["USER", "ADMIN"]);
        var token = s.CreateAccessToken(user);
        Assert.Equal(_clock.Now.UtcDateTime.AddMinutes(15), token.ExpiresAt, TimeSpan.FromSeconds(1));

        var parameters = (string key, string iss, string aud) => new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
            ValidIssuer = iss, ValidAudience = aud, ValidateLifetime = true, RoleClaimType = "role",
            LifetimeValidator = null
        };
        var handler = new JsonWebTokenHandler();

        var ok = await handler.ValidateTokenAsync(token.Token, parameters(Opt.Key, "iss", "aud"));
        Assert.True(ok.IsValid);
        Assert.Equal(user.Id.ToString(), ok.ClaimsIdentity.FindFirst("sub")!.Value);
        Assert.Contains(ok.ClaimsIdentity.Claims, c => c.Type == "role" && c.Value == "ADMIN");

        Assert.False((await handler.ValidateTokenAsync(token.Token, parameters(new string('x', 40), "iss", "aud"))).IsValid);
        Assert.False((await handler.ValidateTokenAsync(token.Token, parameters(Opt.Key, "other", "aud"))).IsValid);
        Assert.False((await handler.ValidateTokenAsync(token.Token, parameters(Opt.Key, "iss", "other"))).IsValid);
    }
}

public class ResourceOwnerHandlerTests
{
    private sealed record Res(Guid OwnerId) : IOwnedResource;

    private static async Task<bool> Check(ClaimsPrincipal user, IOwnedResource res)
    {
        var ctx = new AuthorizationHandlerContext([ResourceOwnerRequirement.Instance], user, res);
        await new ResourceOwnerHandler().HandleAsync(ctx);
        return ctx.HasSucceeded;
    }

    private static ClaimsPrincipal User(Guid id, params string[] roles)
    {
        var claims = new List<Claim> { new("sub", id.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task Owner_is_authorized() { var id = Guid.NewGuid(); Assert.True(await Check(User(id, "USER"), new Res(id))); }

    [Fact]
    public async Task Other_user_is_denied() => Assert.False(await Check(User(Guid.NewGuid(), "USER"), new Res(Guid.NewGuid())));

    [Fact]
    public async Task Admin_is_authorized_on_any_resource() =>
        Assert.True(await Check(User(Guid.NewGuid(), RoleNames.Admin), new Res(Guid.NewGuid())));
}
