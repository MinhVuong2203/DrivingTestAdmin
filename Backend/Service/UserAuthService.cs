using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Backend.Service.Interface;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Service
{
    public class UserAuthService : IUserAuthService
    {
        private const string FirebaseCertUrl =
            "https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com";

        private static readonly SemaphoreSlim CertLock = new(1, 1);
         private static IReadOnlyCollection<SecurityKey>? cachedKeys;
        private static DateTimeOffset cachedKeysUntil = DateTimeOffset.MinValue;

        private readonly HttpClient _httpClient;
        private readonly string _projectId;
        private readonly string _webApiKey;

        public UserAuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _projectId =
                configuration["Firebase:ProjectId"]
                ?? Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID")
                ?? "myapp-8fb3f";
            _webApiKey =
                configuration["Firebase:WebApiKey"]
                ?? Environment.GetEnvironmentVariable("FIREBASE_WEB_API_KEY")
                ?? "AIzaSyDjj4Gu-my6wo2rDuDY9x_RM7_G5616F3U";
        }

        public async Task<UserAuthResult> AuthenticateAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken)
        {
            var authorization = httpContext.Request.Headers.Authorization.ToString();

            if (string.IsNullOrWhiteSpace(authorization))
            {
                return new UserAuthResult(false, StatusCodes.Status401Unauthorized, "Không có quyền");
            }

            try
            {
                string uid;

                if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    var token = authorization["Bearer ".Length..].Trim();

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        return new UserAuthResult(false, StatusCodes.Status401Unauthorized, "Không có quyền");
                    }

                    uid = await VerifyFirebaseTokenAsync(token, cancellationToken);
                }
                else if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    uid = await SignInWithEmailPasswordAsync(authorization, cancellationToken);
                }
                else
                {
                    return new UserAuthResult(false, StatusCodes.Status401Unauthorized, "Không có quyền");
                }

                httpContext.Items["UserUid"] = uid;

                return new UserAuthResult(true, StatusCodes.Status200OK, "OK", uid);
            }
            catch (Exception)
            {
                return new UserAuthResult(
                    false,
                    StatusCodes.Status401Unauthorized,
                    "Không có quyền"
                );
            }
        }

        private async Task<string> SignInWithEmailPasswordAsync(
            string authorization,
            CancellationToken cancellationToken)
        {
            var encodedCredentials = authorization["Basic ".Length..].Trim();
            var decodedCredentials = Encoding.UTF8.GetString(
                Convert.FromBase64String(encodedCredentials)
            );
            var separatorIndex = decodedCredentials.IndexOf(':');

            if (separatorIndex <= 0)
            {
                throw new SecurityTokenException("Invalid basic credentials.");
            }

            var email = decodedCredentials[..separatorIndex];
            var password = decodedCredentials[(separatorIndex + 1)..];

            var url =
                "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword"
                + $"?key={_webApiKey}";

            var payload = JsonSerializer.Serialize(new
            {
                email,
                password,
                returnSecureToken = true
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new SecurityTokenException("Firebase email/password sign in failed.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("localId", out var localIdElement))
            {
                throw new SecurityTokenException("Firebase sign in response does not contain uid.");
            }

            return localIdElement.GetString()
                ?? throw new SecurityTokenException("Firebase sign in uid is empty.");
        }

        private async Task<string> VerifyFirebaseTokenAsync(
            string token,
            CancellationToken cancellationToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            if (!string.Equals(jwt.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
            {
                throw new SecurityTokenInvalidAlgorithmException("Firebase token must use RS256.");
            }

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"https://securetoken.google.com/{_projectId}",
                ValidateAudience = true,
                ValidAudience = _projectId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = await GetFirebaseSigningKeysAsync(cancellationToken),
                ClockSkew = TimeSpan.FromMinutes(5),
            };

            var principal = handler.ValidateToken(token, validationParameters, out _);

            var uid =
                principal.FindFirstValue("user_id")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(uid))
            {
                throw new SecurityTokenException("Firebase token does not contain a uid.");
            }

            return uid;
        }

        private async Task<IReadOnlyCollection<SecurityKey>> GetFirebaseSigningKeysAsync(
            CancellationToken cancellationToken)
        {
            if (cachedKeys is not null && cachedKeysUntil > DateTimeOffset.UtcNow)
            {
                return cachedKeys;
            }

            await CertLock.WaitAsync(cancellationToken);

            try
            {
                if (cachedKeys is not null && cachedKeysUntil > DateTimeOffset.UtcNow)
                {
                    return cachedKeys;
                }

                using var response = await _httpClient.GetAsync(FirebaseCertUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var certs = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();

                cachedKeys = certs.Select(cert =>
                {
                    var certificate = X509Certificate2.CreateFromPem(cert.Value);
                    return (SecurityKey)new X509SecurityKey(certificate)
                    {
                        KeyId = cert.Key
                    };
                }).ToArray();

                var maxAge = response.Headers.CacheControl?.MaxAge ?? TimeSpan.FromMinutes(30);
                cachedKeysUntil = DateTimeOffset.UtcNow.Add(maxAge).AddMinutes(-1);

                return cachedKeys;
            }
            finally
            {
                CertLock.Release();
            }
        }
    }
}
