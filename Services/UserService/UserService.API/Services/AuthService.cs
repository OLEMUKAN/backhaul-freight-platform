using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UserService.API.Data;
using UserService.API.Events;
using UserService.API.Models;
using UserService.API.Models.Dtos;
using UserService.API.Models.Enums;

namespace UserService.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UserDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEventPublisher _eventPublisher;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            UserDbContext context,
            IConfiguration configuration,
            IEventPublisher eventPublisher)
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
            _eventPublisher = eventPublisher;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                throw new UnauthorizedAccessException("Invalid credentials.");
            }

            // Update last login date
            user.LastLoginDate = DateTimeOffset.UtcNow;
            await _userManager.UpdateAsync(user);

            // Publish login event
            await _eventPublisher.PublishAsync(new UserLoginEvent
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Role = user.Role
            });

            return await GenerateTokensAsync(user);
        }

        public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
        {
            var storedRefreshToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedRefreshToken == null)
            {
                throw new SecurityTokenException("Invalid refresh token");
            }

            // Check if token is expired, used, or revoked
            if (storedRefreshToken.ExpiresAt < DateTimeOffset.UtcNow)
            {
                throw new SecurityTokenException("Refresh token has expired");
            }

            if (storedRefreshToken.IsUsed || storedRefreshToken.IsRevoked)
            {
                throw new SecurityTokenException("Refresh token has been used or revoked");
            }

            // Mark current token as used
            storedRefreshToken.IsUsed = true;
            _context.RefreshTokens.Update(storedRefreshToken);
            await _context.SaveChangesAsync();

            // Generate new tokens
            var user = storedRefreshToken.User;
            if (user == null)
            {
                throw new ApplicationException("User not found");
            }

            return await GenerateTokensAsync(user);
        }

        public async Task<bool> RevokeTokenAsync(string refreshToken)
        {
            var storedRefreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedRefreshToken == null)
            {
                return false;
            }

            storedRefreshToken.IsRevoked = true;
            _context.RefreshTokens.Update(storedRefreshToken);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<string> GenerateEmailVerificationTokenAsync(ApplicationUser user)
        {
            return await _userManager.GenerateEmailConfirmationTokenAsync(user);
        }

        public async Task<bool> VerifyEmailAsync(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                var wasAlreadyVerified = user.IsEmailConfirmed;
                user.IsEmailConfirmed = true;
                await _userManager.UpdateAsync(user);
                
                if (!wasAlreadyVerified)
                {
                    // Publish verification event
                    await _eventPublisher.PublishAsync(new UserVerifiedEvent
                    {
                        UserId = user.Id,
                        Email = user.Email ?? string.Empty,
                        Role = user.Role,
                        IsEmailVerified = true,
                        IsPhoneVerified = user.IsPhoneConfirmed
                    });
                }
                
                return true;
            }

            return false;
        }

        public async Task<string> GeneratePasswordResetTokenAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                throw new ApplicationException("User not found");
            }

            return await _userManager.GeneratePasswordResetTokenAsync(user);
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return false;
            }

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result.Succeeded;
        }

        private async Task<LoginResponse> GenerateTokensAsync(ApplicationUser user)
        {
            // Generate JWT Token
            var jwtId = Guid.NewGuid().ToString();
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, jwtId),
                // Convert Role from integer to string name and use standard ClaimTypes.Role
                new Claim(ClaimTypes.Role, GetRoleName(user.Role)),
                // Add numeric role value for systems that expect it
                new Claim("role", ((int)user.Role).ToString()),
                // Add string role name for systems that expect it
                new Claim("roleName", user.Role.ToString()),
                new Claim("name", user.Name ?? string.Empty)
            };

            var keyBytes = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured"));
            var securityKey = new SymmetricSecurityKey(keyBytes);
            // Assign a key ID for the token
            securityKey.KeyId = "auth-token-key-1";
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            
            var tokenExpiration = DateTime.UtcNow.AddMinutes(
                int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60"));
            
            // Create JWT Security Token Descriptor
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                Subject = new ClaimsIdentity(claims),
                NotBefore = DateTime.UtcNow,
                Expires = tokenExpiration,
                SigningCredentials = signingCredentials
            };
            
            // Create and write the token
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(token);

            // Generate Refresh Token
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = GenerateRefreshTokenString(),
                JwtId = jwtId,
                IsUsed = false,
                IsRevoked = false,
                IssuedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(
                    int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7"))
            };
            
            await _context.RefreshTokens.AddAsync(refreshToken);
            await _context.SaveChangesAsync();
            
            return new LoginResponse 
            { 
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = tokenExpiration,
                // Added properties to match Postman expectations
                Token = accessToken,
                UserId = user.Id.ToString()
            };
        }

        private string GenerateRefreshTokenString()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private string GetRoleName(UserRole role)
        {
            // Use the enum extension method but add a fallback in case of error
            try
            {
                return role.ToRoleName();
            }
            catch (Exception)
            {
                // Fallback to a safe default if the extension method fails
                return role.ToString();
            }
        }
    }
}