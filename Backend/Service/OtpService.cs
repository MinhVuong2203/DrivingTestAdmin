using System.Security.Cryptography;
using System.Text;

using Backend.Models;
using Backend.Service.Interface;

using Microsoft.Extensions.Caching.Memory;

namespace Backend.Service
{
    public class OtpService : IOtpService
    {
        private const int OtpExpireMinutes = 5;
        private const int MaxFailedAttempts = 5;

        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly ILogger<OtpService> _logger;

        public OtpService(
            IMemoryCache cache,
            IEmailService emailService,
            ILogger<OtpService> logger
        )
        {
            _cache = cache;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task SendOtp(
            string email,
            CancellationToken cancellationToken = default
        )
        {
            email = NormalizeEmail(email);

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException(
                    "Email không được để trống"
                );
            }

            var otp = GenerateOtp();

            var cacheKey = GetCacheKey(email);

            var otpEntry = new OtpCacheEntry
            {
                OtpHash = HashOtp(email, otp),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(
                    OtpExpireMinutes
                ),
                FailedAttempts = 0
            };

            _cache.Set(
                cacheKey,
                otpEntry,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow =
                        TimeSpan.FromMinutes(
                            OtpExpireMinutes
                        )
                }
            );

            try
            {
                _logger.LogInformation(
                    "Chuẩn bị gửi OTP đến {Email}",
                    email
                );

                await _emailService.SendOtpEmail(
                    email,
                    otp,
                    OtpExpireMinutes,
                    cancellationToken
                );

                _logger.LogInformation(
                    "Đã gửi OTP đến {Email}",
                    email
                );
            }
            catch
            {
                // Nếu gửi email thất bại thì xóa OTP khỏi cache.
                _cache.Remove(cacheKey);
                throw;
            }
        }

        public bool VerifyOtp(
            string email,
            string otp,
            out string message
        )
        {
            email = NormalizeEmail(email);
            otp = otp?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email))
            {
                message = "Email không được để trống";
                return false;
            }

            if (otp.Length != 6 ||
                !otp.All(char.IsDigit))
            {
                message = "OTP phải gồm 6 chữ số";
                return false;
            }

            var cacheKey = GetCacheKey(email);

            if (!_cache.TryGetValue(
                    cacheKey,
                    out OtpCacheEntry? entry
                ) ||
                entry == null)
            {
                message =
                    "OTP không tồn tại hoặc đã hết hạn";

                return false;
            }

            if (DateTime.UtcNow > entry.ExpiresAtUtc)
            {
                _cache.Remove(cacheKey);

                message = "OTP đã hết hạn";
                return false;
            }

            if (entry.FailedAttempts >=
                MaxFailedAttempts)
            {
                _cache.Remove(cacheKey);

                message =
                    "Bạn đã nhập sai OTP quá số lần cho phép";

                return false;
            }

            var submittedHash =
                HashOtp(email, otp);

            var isCorrect =
                CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(
                        entry.OtpHash
                    ),
                    Convert.FromHexString(
                        submittedHash
                    )
                );

            if (!isCorrect)
            {
                entry.FailedAttempts++;

                if (entry.FailedAttempts >=
                    MaxFailedAttempts)
                {
                    _cache.Remove(cacheKey);

                    message =
                        "Bạn đã nhập sai OTP quá số lần cho phép";

                    return false;
                }

                _cache.Set(
                    cacheKey,
                    entry,
                    entry.ExpiresAtUtc
                );

                var remaining =
                    MaxFailedAttempts -
                    entry.FailedAttempts;

                message =
                    $"OTP không chính xác. " +
                    $"Còn {remaining} lần thử.";

                return false;
            }

            // OTP chỉ được dùng một lần.
            _cache.Remove(cacheKey);

            message = "Xác thực OTP thành công";
            return true;
        }

        private static string GenerateOtp()
        {
            return RandomNumberGenerator
                .GetInt32(
                    0,
                    1_000_000
                )
                .ToString("D6");
        }

        private static string HashOtp(
            string email,
            string otp
        )
        {
            var rawValue = $"{email}:{otp}";

            var bytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    rawValue
                )
            );

            return Convert.ToHexString(bytes);
        }

        private static string GetCacheKey(
            string email
        )
        {
            return $"otp:{email}";
        }

        private static string NormalizeEmail(
            string email
        )
        {
            return email?
                       .Trim()
                       .ToLowerInvariant()
                   ?? string.Empty;
        }
    }
}