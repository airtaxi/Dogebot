using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Dogebot.Commons;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Dogebot.Server.Authentication;

public class ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredApiKey = Environment.GetEnvironmentVariable(ApiKeyAuthenticationDefaults.EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            Logger.LogDebug("[API_KEY_AUTH] {EnvironmentVariableName} is not set. Rejecting request to {Path}", ApiKeyAuthenticationDefaults.EnvironmentVariableName, Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured."));
        }

        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var providedApiKeyValues) || providedApiKeyValues.Count == 0)
        {
            Logger.LogDebug("[API_KEY_AUTH] Missing {HeaderName} header on request to {Path} from {RemoteIpAddress}", ApiKeyAuthenticationDefaults.HeaderName, Request.Path, Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedApiKey = providedApiKeyValues.ToString();
        if (!IsApiKeyValid(configuredApiKey, providedApiKey))
        {
            Logger.LogDebug("[API_KEY_AUTH] Invalid API key on request to {Path} from {RemoteIpAddress}", Request.Path, Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        Claim[] claims = [new(ClaimTypes.Name, "DogebotClient")];
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var authenticationTicket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(authenticationTicket));
    }

    private static bool IsApiKeyValid(string configuredApiKey, string providedApiKey)
    {
        var configuredApiKeyBytes = Encoding.UTF8.GetBytes(configuredApiKey);
        var providedApiKeyBytes = Encoding.UTF8.GetBytes(providedApiKey);
        return configuredApiKeyBytes.Length == providedApiKeyBytes.Length && CryptographicOperations.FixedTimeEquals(configuredApiKeyBytes, providedApiKeyBytes);
    }
}
