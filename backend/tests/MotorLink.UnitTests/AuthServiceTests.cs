using Microsoft.Extensions.Options;
using MotorLink.Application.Abstractions;
using MotorLink.Application.Auth;
using MotorLink.Application.Options;
using MotorLink.Domain.Common;
using MotorLink.Domain.Entities;
using MotorLink.Domain.Exceptions;
using NSubstitute;

namespace MotorLink.UnitTests;

public sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public class AuthServiceTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IRefreshTokenRepository _repo = Substitute.For<IRefreshTokenRepository>();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly UserAccount _user = new(Guid.NewGuid(), "a@b.com", "Ana Perez", true, ["USER"]);
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokens.CreateAccessToken(Arg.Any<UserAccount>()).Returns(new AccessToken("jwt", _clock.Now.UtcDateTime.AddMinutes(15)));
        _tokens.GenerateRefreshToken().Returns("new-raw");
        _tokens.HashToken(Arg.Any<string>()).Returns(ci => "hash:" + ci.Arg<string>());
        _identity.FindByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _identity.FindByEmailAsync(_user.Email, Arg.Any<CancellationToken>()).Returns(_user);
        _sut = new AuthService(_identity, _tokens, _repo, Substitute.For<MotorLink.Application.Subscriptions.ISubscriptionPermissions>(), Options.Create(new JwtOptions { RefreshTokenDays = 7 }), _clock);
    }

    private RefreshToken Stored(bool revoked = false, TimeSpan? expiresIn = null) => new()
    {
        Id = Guid.NewGuid(), UserId = _user.Id, FamilyId = Guid.NewGuid(), TokenHash = "hash:old",
        CreatedAt = _clock.Now.UtcDateTime, ExpiresAt = _clock.Now.UtcDateTime + (expiresIn ?? TimeSpan.FromDays(7)),
        RevokedAt = revoked ? _clock.Now.UtcDateTime : null
    };

    [Fact]
    public async Task Login_with_valid_credentials_returns_tokens_and_persists_refresh_token_hash()
    {
        _identity.CheckPasswordAsync(_user.Id, "pw", Arg.Any<CancellationToken>()).Returns(PasswordCheckResult.Success);

        var result = await _sut.LoginAsync(new LoginRequest { Email = _user.Email, Password = "pw" }, "1.2.3.4", default);

        Assert.Equal("jwt", result.AccessToken);
        Assert.Equal("new-raw", result.RefreshToken);
        Assert.Equal(_clock.Now.UtcDateTime.AddDays(7), result.RefreshTokenExpiresAt);
        // Solo se guarda el hash, nunca el token en claro.
        _repo.Received(1).Add(Arg.Is<RefreshToken>(t => t.TokenHash == "hash:new-raw" && t.CreatedByIp == "1.2.3.4"));
    }

    [Fact]
    public async Task Login_with_wrong_password_fails_with_generic_message()
    {
        _identity.CheckPasswordAsync(_user.Id, "bad", Arg.Any<CancellationToken>()).Returns(PasswordCheckResult.Failed);
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest { Email = _user.Email, Password = "bad" }, null, default));
        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    [Fact]
    public async Task Login_with_unknown_email_gives_same_error_as_wrong_password()
    {
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest { Email = "nobody@x.com", Password = "x" }, null, default));
        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    [Fact]
    public async Task Login_when_locked_out_throws_AccountLocked()
    {
        _identity.CheckPasswordAsync(_user.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(PasswordCheckResult.LockedOut);
        await Assert.ThrowsAsync<AccountLockedException>(() =>
            _sut.LoginAsync(new LoginRequest { Email = _user.Email, Password = "x" }, null, default));
    }

    [Fact]
    public async Task Refresh_rotates_token_revoking_old_and_linking_replacement()
    {
        var old = Stored();
        _repo.FindByHashAsync("hash:old", Arg.Any<CancellationToken>()).Returns(old);

        var result = await _sut.RefreshAsync(new RefreshRequest { RefreshToken = "old" }, null, default);

        Assert.Equal("new-raw", result.RefreshToken);
        Assert.True(old.IsRevoked);
        Assert.Equal(RefreshTokenRevocationReason.Rotated, old.RevocationReason);
        Assert.Equal("hash:new-raw", old.ReplacedByTokenHash);
        // la nueva token conserva la familia
        _repo.Received(1).Add(Arg.Is<RefreshToken>(t => t.FamilyId == old.FamilyId));
    }

    [Fact]
    public async Task Refresh_with_revoked_token_revokes_entire_family()
    {
        var old = Stored(revoked: true);
        _repo.FindByHashAsync("hash:old", Arg.Any<CancellationToken>()).Returns(old);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.RefreshAsync(new RefreshRequest { RefreshToken = "old" }, null, default));

        await _repo.Received(1).RevokeFamilyAsync(old.FamilyId, RefreshTokenRevocationReason.ReuseDetected,
            _clock.Now.UtcDateTime, Arg.Any<CancellationToken>());
        _repo.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Refresh_with_expired_token_fails()
    {
        _repo.FindByHashAsync("hash:old", Arg.Any<CancellationToken>()).Returns(Stored(expiresIn: TimeSpan.FromSeconds(-1)));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.RefreshAsync(new RefreshRequest { RefreshToken = "old" }, null, default));
    }

    [Fact]
    public async Task Refresh_with_unknown_token_fails()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _sut.RefreshAsync(new RefreshRequest { RefreshToken = "nope" }, null, default));
    }

    [Fact]
    public async Task Logout_revokes_family_and_is_idempotent_for_unknown_tokens()
    {
        var stored = Stored();
        _repo.FindByHashAsync("hash:old", Arg.Any<CancellationToken>()).Returns(stored);

        await _sut.LogoutAsync(new LogoutRequest { RefreshToken = "old" }, default);
        await _repo.Received(1).RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevocationReason.Logout, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        await _sut.LogoutAsync(new LogoutRequest { RefreshToken = "unknown" }, default); // no lanza
    }

    [Fact]
    public async Task Logout_all_sessions_revokes_every_token_of_the_user()
    {
        var stored = Stored();
        _repo.FindByHashAsync("hash:old", Arg.Any<CancellationToken>()).Returns(stored);
        await _sut.LogoutAsync(new LogoutRequest { RefreshToken = "old", AllSessions = true }, default);
        await _repo.Received(1).RevokeAllForUserAsync(_user.Id, RefreshTokenRevocationReason.Logout, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_revokes_all_sessions()
    {
        await _sut.ChangePasswordAsync(_user.Id, new ChangePasswordRequest { CurrentPassword = "a", NewPassword = "Bbbbbbb1" }, default);
        await _repo.Received(1).RevokeAllForUserAsync(_user.Id, RefreshTokenRevocationReason.PasswordChanged, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Business_registration_requires_business_name()
    {
        var req = new RegisterRequest { Email = "a@b.com", Password = "Abcdef12", FirstName = "A", LastName = "B", ProfileType = ProfileType.Business };
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var ok = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            req, new System.ComponentModel.DataAnnotations.ValidationContext(req), results, validateAllProperties: true);
        Assert.False(ok);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterRequest.BusinessName)));
    }
}
