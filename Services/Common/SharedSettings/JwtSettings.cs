using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Hosting;

namespace SharedSettings
{
    public static class JwtSettings
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT:Key not configured");
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
                securityKey.KeyId = "auth-token-key-1"; // Must match the KeyId used for signing
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = securityKey,
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                    RequireSignedTokens = true,
                    ValidateActor = false,
                    ValidateTokenReplay = false,
                    ClockSkew = TimeSpan.FromMinutes(5) // Add clock skew tolerance
                };
                
                // Add event handlers for better debugging and logging
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        
                        // Log more details about the error
                        if (context.Exception is SecurityTokenValidationException)
                        {
                            logger.LogWarning("JWT validation error: {Error}", context.Exception.ToString());
                        }
                        else
                        {
                            logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                        }
                        
                        // Log token details for debugging in development
                        if (context.HttpContext.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true)
                        {
                            if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
                            {
                                var token = authHeader.ToString().Replace("Bearer ", "");
                                if (!string.IsNullOrEmpty(token))
                                {
                                    try {
                                        var handler = new JwtSecurityTokenHandler();
                                        if (handler.CanReadToken(token))
                                        {
                                            var jwtToken = handler.ReadJwtToken(token);
                                            logger.LogDebug("Token issuer: {Issuer}, audience: {Audience}, expiration: {Expiration}", 
                                                jwtToken.Issuer, string.Join(",", jwtToken.Audiences), jwtToken.ValidTo);
                                            
                                            // Log all claims in the token
                                            foreach (var claim in jwtToken.Claims)
                                            {
                                                logger.LogDebug("Token claim: {Type} = {Value}", claim.Type, claim.Value);
                                            }
                                            
                                            // Log signature algorithm and other header data
                                            logger.LogDebug("Token algorithm: {Algorithm}, headers: {Headers}", 
                                                jwtToken.Header.Alg, 
                                                string.Join(", ", jwtToken.Header.Select(h => $"{h.Key}={h.Value}")));
                                        }
                                    }
                                    catch (Exception ex) {
                                        logger.LogDebug("Error parsing JWT token: {Error}", ex.Message);
                                    }
                                }
                            }
                        }
                        
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogInformation("Token validated for user {UserId} with roles: {Roles}", 
                            context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown",
                            string.Join(", ", context.Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Array.Empty<string>()));
                        return Task.CompletedTask;
                    }
                };
            });
            
            return services;
        }
    }
}