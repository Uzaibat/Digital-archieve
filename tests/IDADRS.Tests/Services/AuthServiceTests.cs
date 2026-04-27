using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Application.Services;
using IDADRS.Core.Entities;
using IDADRS.Core.Enums;
using IDADRS.Core.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IDADRS.Tests.Services;

public sealed class AuthServiceTests
{
    private readonly Mock<IUnitOfWork>              _uow    = new();
    private readonly Mock<IUserRepository>          _users  = new();
    private readonly Mock<IRefreshTokenRepository>  _tokens = new();
    private readonly Mock<IJwtService>              _jwt    = new();
    private readonly AuthService                    _sut;

    public AuthServiceTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _uow.Setup(u => u.RefreshTokens).Returns(_tokens.Object);
        _sut = new AuthService(_uow.Object, _jwt.Object, NullLogger<AuthService>.Instance);
    }

    [Fact] public async Task RegisterAsync_NewUser_ReturnsSuccess()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("alice", default)).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync("alice@test.com", default)).ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveAsync(default)).ReturnsAsync(1);
        var r = await _sut.RegisterAsync(new RegisterRequestDto("alice", "alice@test.com", "Password123!"));
        Assert.True(r.Success);
        Assert.Equal("alice", r.Data!.Username);
    }

    [Fact] public async Task RegisterAsync_DuplicateUsername_ReturnsFail()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("alice", default)).ReturnsAsync(true);
        var r = await _sut.RegisterAsync(new RegisterRequestDto("alice", "x@y.com", "Password123!"));
        Assert.False(r.Success);
    }

    [Fact] public async Task LoginAsync_WrongPassword_ReturnsFail()
    {
        _users.Setup(r => r.GetByUsernameAsync("alice", default)).ReturnsAsync(new User
        {
            Id = 1, Username = "alice", Email = "a@b.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass1!", workFactor: 4),
            Role = UserRole.User,
        });
        var r = await _sut.LoginAsync(new LoginRequestDto("alice", "WrongPass"));
        Assert.False(r.Success);
    }

    [Fact] public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var user = new User
        {
            Id = 5, Username = "bob", Email = "b@c.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", workFactor: 4),
            Role = UserRole.Archivist,
        };
        _users.Setup(r => r.GetByUsernameAsync("bob", default)).ReturnsAsync(user);
        _jwt.Setup(j => j.GenerateAccessToken(user)).Returns("access.token");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh.token");
        _jwt.Setup(j => j.AccessTokenExpiry()).Returns(DateTime.UtcNow.AddMinutes(15));
        _jwt.Setup(j => j.RefreshTokenExpiry()).Returns(DateTime.UtcNow.AddDays(7));
        _tokens.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default)).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveAsync(default)).ReturnsAsync(1);
        var r = await _sut.LoginAsync(new LoginRequestDto("bob", "Password123!"));
        Assert.True(r.Success);
        Assert.Equal("access.token", r.Data!.AccessToken);
    }
}
