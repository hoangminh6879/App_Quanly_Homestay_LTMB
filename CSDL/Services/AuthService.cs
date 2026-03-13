using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Nhom1.DTOs;
using Nhom1.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Nhom1.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDto?> LoginAsync(LoginDto loginDto);
        Task<LoginResponseDto?> LoginWithTwoFactorAsync(TwoFactorLoginDto twoFactorLoginDto);
        Task<LoginResponseDto?> LoginWithGoogleAsync(string idToken, string googleClientId);
        Task<UserDto?> RegisterAsync(RegisterDto registerDto);
        Task<LoginResponseDto?> RefreshTokenAsync(RefreshTokenDto refreshTokenDto);
        Task<bool> LogoutAsync(string userId);
        Task<User?> FindByEmailAsync(string email);
        Task<string> GeneratePasswordResetTokenAsync(User user);
    // Email OTP methods
    Task<bool> SendLoginOtpAsync(string email);
    Task<LoginResponseDto?> VerifyLoginOtpAsync(string email, string code);
        
        // Two-Factor Authentication methods
    Task<TwoFactorSetupDto?> EnableTwoFactorAsync(string userId, string password);
    Task<bool> EnableTwoFactorByEmailAsync(string userId);
        Task<bool> VerifyTwoFactorTokenAsync(string userId, string code, bool rememberMachine);
        Task<bool> DisableTwoFactorAsync(string userId, string password);
        Task<bool> IsTwoFactorEnabledAsync(string userId);
    }

    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _distributedCache;
        private readonly ILogger<AuthService> _logger;

    // In-memory fallback store used when distributed cache (Redis) becomes unavailable at runtime.
    // Key -> (value, expiration)
    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset? ExpiresAt)> _localFallbackStore = new();

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration configuration,
            IDistributedCache distributedCache,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _distributedCache = distributedCache;
            _logger = logger;
        }

        // Helper: set string with fallback
        private async Task SetCacheStringAsync(string key, string value, DistributedCacheEntryOptions? options = null)
        {
            try
            {
                if (options is null)
                    await _distributedCache.SetStringAsync(key, value);
                else
                    await _distributedCache.SetStringAsync(key, value, options);
            }
            catch (Exception ex)
            {
                try
                {
                    _logger.LogWarning(ex, "Distributed cache set failed for key {Key}, using in-memory fallback.", key);
                    DateTimeOffset? expiresAt = null;
                    if (options != null && options.AbsoluteExpirationRelativeToNow.HasValue)
                    {
                        expiresAt = DateTimeOffset.UtcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
                    }

                    _localFallbackStore[key] = (value, expiresAt);
                }
                catch (Exception inner)
                {
                    _logger.LogError(inner, "Failed to set local fallback cache for key {Key}", key);
                }
            }
        }

        // Helper: get string with fallback
        private async Task<string?> GetCacheStringAsync(string key)
        {
            try
            {
                return await _distributedCache.GetStringAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Distributed cache get failed for key {Key}, reading from in-memory fallback.", key);
                if (_localFallbackStore.TryGetValue(key, out var entry))
                {
                    if (entry.ExpiresAt == null || entry.ExpiresAt > DateTimeOffset.UtcNow)
                        return entry.Value;

                    // expired
                    _localFallbackStore.TryRemove(key, out _);
                }

                return null;
            }
        }

        // Helper: remove string with fallback
        private async Task RemoveCacheKeyAsync(string key)
        {
            try
            {
                await _distributedCache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Distributed cache remove failed for key {Key}, removing from in-memory fallback.", key);
                _localFallbackStore.TryRemove(key, out _);
            }
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);
            if (user == null || !user.IsActive)
                return null;

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);
            if (!result.Succeeded)
                return null;

            // Check if 2FA is enabled for this user (flag stored in the user record).
            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                // Return special response indicating 2FA is required
                return new LoginResponseDto
                {
                    Token = "2FA_REQUIRED",
                    RefreshToken = "",
                    Expiration = DateTime.UtcNow,
                    User = MapToUserDto(user)
                };
            }

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();
            
            // Store refresh token in distributed cache (key per user)
            await SetCacheStringAsync($"refresh:{user.Id}", refreshToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            });

            return new LoginResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                Expiration = DateTime.UtcNow.AddHours(24),
                User = MapToUserDto(user)
            };
        }

        public async Task<UserDto?> RegisterAsync(RegisterDto registerDto)
        {
            var user = new User
            {
                UserName = registerDto.Email,
                Email = registerDto.Email,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                PhoneNumber = registerDto.PhoneNumber,
                EmailConfirmed = true, // For simplicity, auto-confirm
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);
            if (!result.Succeeded)
                return null;

            // Assign role based on registration choice
            var role = registerDto.Role == "Host" ? UserRoles.Host : UserRoles.User;
            await _userManager.AddToRoleAsync(user, role);

            return MapToUserDto(user);
        }

        public async Task<LoginResponseDto?> LoginWithGoogleAsync(string idToken, string googleClientId)
        {
            try
            {
                var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken, new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { googleClientId }
                });

                if (payload == null)
                    return null;

                // Find user by email
                var user = await _userManager.FindByEmailAsync(payload.Email);
                if (user == null)
                {
                    // Create new user
                    user = new User
                    {
                        UserName = payload.Email,
                        Email = payload.Email,
                        FirstName = payload.GivenName,
                        LastName = payload.FamilyName,
                        EmailConfirmed = true,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    var result = await _userManager.CreateAsync(user);
                    if (!result.Succeeded)
                        return null;

                    // assign default role
                    await _userManager.AddToRoleAsync(user, UserRoles.User);
                }

                // proceed to generate JWT
                var token = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();
                await SetCacheStringAsync($"refresh:{user.Id}", refreshToken, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                });

                return new LoginResponseDto
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    Expiration = DateTime.UtcNow.AddHours(24),
                    User = MapToUserDto(user)
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<LoginResponseDto?> RefreshTokenAsync(RefreshTokenDto refreshTokenDto)
        {
            var principal = GetPrincipalFromExpiredToken(refreshTokenDto.Token);
            if (principal == null)
                return null;

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return null;

            // Verify refresh token from distributed cache
            var storedRefreshToken = await GetCacheStringAsync($"refresh:{userId}");
            if (string.IsNullOrEmpty(storedRefreshToken) || storedRefreshToken != refreshTokenDto.RefreshToken)
                return null;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !user.IsActive)
                return null;

            var newToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();
            
            await SetCacheStringAsync($"refresh:{userId}", newRefreshToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            });

            return new LoginResponseDto
            {
                Token = newToken,
                RefreshToken = newRefreshToken,
                Expiration = DateTime.UtcNow.AddHours(24),
                User = MapToUserDto(user)
            };
        }

        public async Task<bool> LogoutAsync(string userId)
        {
            await RemoveCacheKeyAsync($"refresh:{userId}");
            return true;
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Add user roles to claims
            var roles = _userManager.GetRolesAsync(user).Result;
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]!)),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;

                return principal;
            }
            catch
            {
                return null;
            }
        }

        public async Task<User?> FindByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<string> GeneratePasswordResetTokenAsync(User user)
        {
            return await _userManager.GeneratePasswordResetTokenAsync(user);
        }

        private UserDto MapToUserDto(User user)
        {
            var roles = _userManager.GetRolesAsync(user).Result;
            
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                ProfilePicture = user.ProfilePicture,
                Address = user.Address,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                Roles = roles.ToList()
            };
        }

        public async Task<TwoFactorSetupDto?> EnableTwoFactorAsync(string userId, string password)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return null;

            // Validate password
            var passwordValid = await _userManager.CheckPasswordAsync(user, password);
            if (!passwordValid)
                return null;

            // Generate and set the recovery codes
            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            user.TwoFactorEnabled = true;
            await _userManager.UpdateAsync(user);

            return new TwoFactorSetupDto
            {
                RecoveryCodes = recoveryCodes?.ToList() ?? new List<string>()
            };
        }

        // Enable 2FA using Email OTP (no authenticator key required)
        public async Task<bool> EnableTwoFactorByEmailAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            user.TwoFactorEnabled = true;
            await _userManager.UpdateAsync(user);
            return true;
        }

        public async Task<bool> VerifyTwoFactorTokenAsync(string userId, string code, bool rememberMachine)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(code, rememberMachine, false);
            return result.Succeeded;
        }

        public async Task<bool> DisableTwoFactorAsync(string userId, string password)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            // Validate password
            var passwordValid = await _userManager.CheckPasswordAsync(user, password);
            if (!passwordValid)
                return false;

            user.TwoFactorEnabled = false;
            await _userManager.UpdateAsync(user);
            return true;
        }

        public async Task<bool> IsTwoFactorEnabledAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user?.TwoFactorEnabled ?? false;
        }

        public async Task<LoginResponseDto?> LoginWithTwoFactorAsync(TwoFactorLoginDto twoFactorLoginDto)
        {
            var user = await _userManager.FindByEmailAsync(twoFactorLoginDto.Email);
            if (user == null || !user.IsActive)
                return null;

            var result = await _signInManager.CheckPasswordSignInAsync(user, twoFactorLoginDto.Password, false);
            if (!result.Succeeded)
                return null;

            // Verify 2FA code
            var twoFactorResult = await _signInManager.TwoFactorAuthenticatorSignInAsync(
                twoFactorLoginDto.TwoFactorCode,
                twoFactorLoginDto.RememberMachine,
                false);

            if (!twoFactorResult.Succeeded)
                return null;

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // Store refresh token in distributed cache (with runtime fallback)
            await SetCacheStringAsync($"refresh:{user.Id}", refreshToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            });

            return new LoginResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                Expiration = DateTime.UtcNow.AddHours(24),
                User = MapToUserDto(user)
            };
        }

        // Email OTP implementation
        public async Task<bool> SendLoginOtpAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.IsActive)
                return false;

            // Generate 6-digit code
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var code = (BitConverter.ToUInt32(bytes, 0) % 1000000).ToString("D6");

            var cacheKey = $"emailotp:{email.ToLowerInvariant()}";
            // store code with 5 minutes expiry in distributed cache (with runtime fallback)
            await SetCacheStringAsync(cacheKey, code, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            // Try sending email if SMTP/EmailSettings configured
            try
            {
                // Prefer EmailSettings block if present
                var emailEnabled = _configuration["EmailSettings:EnableEmailSending"];
                var enableEmail = false;
                if (!string.IsNullOrEmpty(emailEnabled) && bool.TryParse(emailEnabled, out var parsed))
                    enableEmail = parsed;

                string smtpHost = null;
                string smtpPortRaw = null;
                string smtpUser = null;
                string smtpPass = null;
                string fromEmail = null;
                string fromName = null;

                if (enableEmail)
                {
                    smtpHost = _configuration["EmailSettings:SmtpServer"];
                    smtpPortRaw = _configuration["EmailSettings:SmtpPort"];
                    smtpUser = _configuration["EmailSettings:SmtpUsername"];
                    smtpPass = _configuration["EmailSettings:SmtpPassword"];
                    fromEmail = _configuration["EmailSettings:SenderEmail"];
                    fromName = _configuration["EmailSettings:SenderName"];
                }

                // Fallback to old Smtp section for any missing values
                smtpHost ??= _configuration["Smtp:Host"] ?? _configuration["Smtp:SmtpServer"] ?? _configuration["Smtp:Server"];
                smtpPortRaw ??= _configuration["Smtp:Port"] ?? _configuration["Smtp:SmtpPort"];
                smtpUser ??= _configuration["Smtp:Username"] ?? _configuration["Smtp:User"];
                smtpPass ??= _configuration["Smtp:Password"];
                fromEmail ??= _configuration["Smtp:FromEmail"] ?? _configuration["Smtp:From"];
                fromName ??= _configuration["Smtp:FromName"];

                if (!string.IsNullOrEmpty(smtpHost))
                {
                    var smtpPort = 25;
                    if (!string.IsNullOrEmpty(smtpPortRaw) && int.TryParse(smtpPortRaw, out var p)) smtpPort = p;

                    using var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort);
                    if (!string.IsNullOrEmpty(smtpUser))
                    {
                        client.Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass);
                        client.EnableSsl = true;
                    }

                    var fromAddress = !string.IsNullOrEmpty(fromName)
                        ? new System.Net.Mail.MailAddress(fromEmail ?? "noreply@example.com", fromName)
                        : new System.Net.Mail.MailAddress(fromEmail ?? "noreply@example.com");

                    var mail = new System.Net.Mail.MailMessage()
                    {
                        From = fromAddress,
                        Subject = "Your login code",
                        Body = $"Your login code is: {code}. It expires in 5 minutes.",
                        IsBodyHtml = false
                    };
                    mail.To.Add(email);

                    await client.SendMailAsync(mail);
                    return true;
                }

                // No SMTP configured - log for dev and return true
                _logger.LogInformation("OTP for {Email}: {Code}", email, code);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP to {Email}", email);
                return false;
            }
        }

        public async Task<LoginResponseDto?> VerifyLoginOtpAsync(string email, string code)
        {
            var cacheKey = $"emailotp:{email.ToLowerInvariant()}";
            var stored = await GetCacheStringAsync(cacheKey);
            if (string.IsNullOrEmpty(stored))
                return null;

            if (stored != code)
                return null;

            // OTP verified - remove it
            await RemoveCacheKeyAsync(cacheKey);

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null || !user.IsActive)
                return null;

            var token = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            // Store refresh token in distributed cache (shared between instances, with runtime fallback)
            await SetCacheStringAsync($"refresh:{user.Id}", refreshToken, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
            });

            return new LoginResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                Expiration = DateTime.UtcNow.AddHours(24),
                User = MapToUserDto(user)
            };
        }
    }
}
