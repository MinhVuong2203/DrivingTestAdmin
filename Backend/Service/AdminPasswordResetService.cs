using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Backend.DTO;
using Backend.Service.Interface;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;

namespace Backend.Service
{
    public class AdminPasswordResetService : IAdminPasswordResetService
    {
        private const int OtpLength = 6;
        private const int OtpMinutes = 10;
        private const int MaxVerifyAttempts = 5;
        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(OtpMinutes);
        private static readonly ConcurrentDictionary<string, OtpState> OtpStore = new();

        private readonly IConfiguration _configuration;
        private readonly FirestoreDb _db;

        public AdminPasswordResetService(IConfiguration configuration, FirestoreDb db)
        {
            _configuration = configuration;
            _db = db;
        }

        public async Task RequestOtpAsync(
            AdminPasswordResetOtpRequest request,
            CancellationToken cancellationToken)
        {
            var email = NormalizeEmail(request.Email);
            if (email == null)
            {
                return;
            }

            var adminUser = await FindActiveAdminAsync(email, cancellationToken);
            if (adminUser == null)
            {
                return;
            }

            var otp = GenerateOtp();
            var salt = GenerateSalt();
            var expiresAt = DateTimeOffset.UtcNow.Add(OtpLifetime);

            var otpState = new OtpState(
                adminUser.Uid,
                HashOtp(otp, salt),
                salt,
                expiresAt,
                0
            );

            await SendOtpEmailAsync(email, otp, cancellationToken);
            OtpStore[email] = otpState;
        }

        public async Task<AdminPasswordResetResult> ResetPasswordAsync(
            AdminPasswordResetConfirmRequest request,
            CancellationToken cancellationToken)
        {
            var email = NormalizeEmail(request.Email);
            var otp = request.Otp?.Trim();
            var newPassword = request.NewPassword;

            if (email == null || string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(newPassword))
            {
                return new AdminPasswordResetResult(false, "Vui lòng nhập đầy đủ email, OTP và mật khẩu mới.");
            }

            if (newPassword.Length < 6)
            {
                return new AdminPasswordResetResult(false, "Mật khẩu mới phải có ít nhất 6 ký tự.");
            }

            if (!OtpStore.TryGetValue(email, out var state))
            {
                return new AdminPasswordResetResult(false, "OTP không hợp lệ hoặc đã hết hạn.");
            }

            if (state.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                OtpStore.TryRemove(email, out _);
                return new AdminPasswordResetResult(false, "OTP đã hết hạn. Vui lòng gửi lại mã mới.");
            }

            if (state.Attempts >= MaxVerifyAttempts)
            {
                OtpStore.TryRemove(email, out _);
                return new AdminPasswordResetResult(false, "Bạn đã nhập sai OTP quá số lần cho phép. Vui lòng gửi lại mã mới.");
            }

            if (!FixedTimeEquals(HashOtp(otp, state.Salt), state.OtpHash))
            {
                OtpStore[email] = state with { Attempts = state.Attempts + 1 };
                return new AdminPasswordResetResult(false, "OTP không đúng.");
            }

            var adminUser = await FindActiveAdminAsync(email, cancellationToken);
            if (adminUser == null || adminUser.Uid != state.Uid)
            {
                OtpStore.TryRemove(email, out _);
                return new AdminPasswordResetResult(false, "Tài khoản admin không hợp lệ hoặc đang bị khóa.");
            }

            await FirebaseAuth.DefaultInstance.UpdateUserAsync(new UserRecordArgs
            {
                Uid = state.Uid,
                Password = newPassword
            }, cancellationToken);

            await _db.Collection("users").Document(state.Uid).SetAsync(
                new Dictionary<string, object>
                {
                    ["updatedAt"] = FieldValue.ServerTimestamp
                },
                SetOptions.MergeAll,
                cancellationToken
            );

            OtpStore.TryRemove(email, out _);
            return new AdminPasswordResetResult(true, "Đổi mật khẩu thành công.");
        }

        private async Task<UserRecord?> FindActiveAdminAsync(string email, CancellationToken cancellationToken)
        {
            UserRecord firebaseUser;
            try
            {
                firebaseUser = await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(email, cancellationToken);
            }
            catch
            {
                return null;
            }

            var userSnapshot = await _db
                .Collection("users")
                .Document(firebaseUser.Uid)
                .GetSnapshotAsync(cancellationToken);

            if (!userSnapshot.Exists)
            {
                return null;
            }

            var data = userSnapshot.ToDictionary();
            var role = ReadString(data, "role");
            var status = ReadString(data, "status") ?? "active";

            if (!string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return firebaseUser;
        }

        private async Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken cancellationToken)
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];
            var portValue = _configuration["Email:SmtpPort"];

            if (string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                !int.TryParse(portValue, out var smtpPort))
            {
                throw new InvalidOperationException("Chưa cấu hình Email SMTP hợp lệ trong appsettings.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(username, "Kiến thức lái xe 600"),
                Subject = "Mã OTP đặt lại mật khẩu admin",
                Body = BuildEmailBody(otp),
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var smtpClient = new SmtpClient(smtpServer, smtpPort)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            await smtpClient.SendMailAsync(message, cancellationToken);
        }

        private static string BuildEmailBody(string otp)
        {
            return $"""
                <div style="font-family:Arial,sans-serif;line-height:1.6;color:#0f172a">
                  <h2>Đặt lại mật khẩu Admin</h2>
                  <p>Mã OTP của bạn là:</p>
                  <p style="font-size:28px;font-weight:700;letter-spacing:6px">{otp}</p>
                  <p>Mã có hiệu lực trong {OtpMinutes} phút. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
                </div>
                """;
        }

        private static string? NormalizeEmail(string? email)
        {
            var normalizedEmail = email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return null;
            }

            try
            {
                _ = new MailAddress(normalizedEmail);
                return normalizedEmail;
            }
            catch
            {
                return null;
            }
        }

        private static string GenerateOtp()
        {
            var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return value.ToString($"D{OtpLength}");
        }

        private static string GenerateSalt()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        }

        private static string HashOtp(string otp, string salt)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}:{otp}"));
            return Convert.ToBase64String(bytes);
        }

        private static bool FixedTimeEquals(string first, string second)
        {
            var firstBytes = Encoding.UTF8.GetBytes(first);
            var secondBytes = Encoding.UTF8.GetBytes(second);
            return firstBytes.Length == secondBytes.Length &&
                CryptographicOperations.FixedTimeEquals(firstBytes, secondBytes);
        }

        private static string? ReadString(IReadOnlyDictionary<string, object> data, string key)
        {
            foreach (var item in data)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value?.ToString();
                }
            }

            return null;
        }

        private sealed record OtpState(
            string Uid,
            string OtpHash,
            string Salt,
            DateTimeOffset ExpiresAt,
            int Attempts);
    }
}
