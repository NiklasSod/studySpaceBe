using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace lmsPortalBe.Services
{
    public interface IEmailSender
    {
        /// <summary>
        /// Sends the "reset your password" email containing a link the user can
        /// follow to choose a new password.
        /// </summary>
        Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Delivers transactional email through the Resend HTTP API (https://resend.com),
    /// which only needs an API key and works from hosts that block outbound SMTP
    /// (such as Railway).
    /// </summary>
    public class ResendEmailSender : IEmailSender
    {
        private const string ApiBaseUrl = "https://api.resend.com";

        private readonly ILogger<ResendEmailSender> _logger;
        private readonly HttpClient _httpClient;
        private readonly Lazy<ResendOptions> _options;

        public ResendEmailSender(IConfiguration configuration, ILogger<ResendEmailSender> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            // Resolved lazily so the application can still boot without credentials;
            // only actually sending an email requires them.
            _options = new Lazy<ResendOptions>(() => ResendOptions.FromConfiguration(configuration));
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
        {
            var options = _options.Value;

            var payload = new SendEmailRequest
            {
                From = options.From,
                To = [toEmail],
                Subject = "Reset your StudySpace password",
                Html = $"""
                    <p>We received a request to reset your StudySpace password.</p>
                    <p><a href="{resetLink}">Choose a new password</a></p>
                    <p>If you did not request this, you can safely ignore this email.</p>
                    """,
                Text = $"""
                    We received a request to reset your StudySpace password.

                    Choose a new password here: {resetLink}

                    If you did not request this, you can safely ignore this email.
                    """
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            request.Content = JsonContent.Create(payload);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Resend request failed ({(int)response.StatusCode}): {body}");
            }

            _logger.LogInformation("Password reset email accepted by Resend for {Email}.", toEmail);
        }

        private sealed class ResendOptions
        {
            public required string ApiKey { get; init; }
            public required string From { get; init; }

            public static ResendOptions FromConfiguration(IConfiguration configuration)
            {
                var apiKey = configuration["RESEND_API_KEY"];
                var from = configuration["EMAIL_FROM"];

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from))
                {
                    throw new InvalidOperationException(
                        "Email delivery is not configured. Set RESEND_API_KEY and EMAIL_FROM.");
                }

                return new ResendOptions { ApiKey = apiKey, From = from };
            }
        }

        private sealed class SendEmailRequest
        {
            [JsonPropertyName("from")]
            public required string From { get; init; }

            [JsonPropertyName("to")]
            public required IReadOnlyList<string> To { get; init; }

            [JsonPropertyName("subject")]
            public required string Subject { get; init; }

            [JsonPropertyName("html")]
            public required string Html { get; init; }

            [JsonPropertyName("text")]
            public required string Text { get; init; }
        }
    }
}
