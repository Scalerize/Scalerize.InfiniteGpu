using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace InfiniteGPU.Backend.Shared.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string textBody, string? htmlBody = null, CancellationToken cancellationToken = default);
}

public sealed class MailgunOptions
{
    public string Domain { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.mailgun.net/v3";
    public string From { get; set; } = string.Empty;
}

public sealed class MailgunEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly MailgunOptions _options;
    private readonly ILogger<MailgunEmailSender> _logger;

    public MailgunEmailSender(
        HttpClient httpClient,
        IOptions<MailgunOptions> options,
        ILogger<MailgunEmailSender> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Mailgun API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.Domain))
        {
            throw new InvalidOperationException("Mailgun domain is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.From))
        {
            _options.From = $"postmaster@{_options.Domain}";
        }
    }

    public async Task SendAsync(string to, string subject, string textBody, string? htmlBody = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            throw new ArgumentNullException(nameof(to));
        }

        var requestUri = BuildRequestUri();
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

        var keyValues = new List<KeyValuePair<string, string>>
        {
            new("from", _options.From),
            new("to", to),
            new("subject", subject),
            new("text", textBody)
        };

        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            keyValues.Add(new KeyValuePair<string, string>("html", htmlBody));
        }

        request.Content = new FormUrlEncodedContent(keyValues);
        request.Headers.Authorization = BuildAuthorizationHeader();

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Mailgun email send failed with status {StatusCode}: {Body}", response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }
    }

    private string BuildRequestUri()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.mailgun.net/v3"
            : _options.BaseUrl.TrimEnd('/');

        return $"{baseUrl}/{_options.Domain}/messages";
    }

    private AuthenticationHeaderValue BuildAuthorizationHeader()
    {
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{_options.ApiKey}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }
}