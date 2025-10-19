using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FtpMcpServer.Auth
{
    /// <summary>
    /// Simple API key (Bearer) authentication that expects a base64-encoded JSON payload with FTP connection details.
    /// The token can be provided via "Authorization: Bearer {token}" or "X-Api-Key: {token}" headers.
    /// </summary>
    public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "ApiKey";

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            // Include a helpful header for clients
            Response.Headers.Append("WWW-Authenticate", "Bearer realm=\"FtpMcpServer\", charset=\"UTF-8\"");
            return base.HandleChallengeAsync(properties);
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var http = Context;
            if (http is null)
            {
                return Task.FromResult(AuthenticateResult.Fail("No HTTP context."));
            }

            string? token = null;
            if (Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
            {
                var authHeader = authHeaderValues.ToString();
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(token) && Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeaderValues))
            {
                token = apiKeyHeaderValues.ToString().Trim();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing Authorization: Bearer or X-Api-Key header."));
            }

            if (!ApiKeyToken.TryParseFromBase64(token, out var payload) || payload == null)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API key token. Expecting base64-encoded JSON."));
            }

            string host = payload.Host ?? payload.Server ?? string.Empty;
            if (string.IsNullOrWhiteSpace(host))
            {
                return Task.FromResult(AuthenticateResult.Fail("Token must include 'server' or 'host'."));
            }

            var identity = payload.ToClaimsIdentity(Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}