using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FtpMcpServer.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FtpMcpServer.Controllers
{
    [ApiController]
    [Route("token")]
    public sealed class TokenController : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GenerateToken([FromQuery] GenerateTokenRequest request)
        {
            if (request is null)
            {
                return BadRequest("Query string is required.");
            }

            string? host = string.IsNullOrWhiteSpace(request.Host) ? request.Server : request.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                return BadRequest("Query string must include 'server' (or 'host').");
            }

            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest("Query string must include 'username'.");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Query string must include 'password'.");
            }

            string directory = string.IsNullOrWhiteSpace(request.Dir) ? "/" : request.Dir!;

            int port = request.Port ?? 21;
            if (port <= 0)
            {
                return BadRequest("Port must be a positive integer.");
            }

            int? timeoutSeconds = request.TimeoutSeconds;
            if (timeoutSeconds.HasValue && timeoutSeconds.Value <= 0)
            {
                return BadRequest("timeoutSeconds must be greater than zero when provided.");
            }

            var payload = new ApiKeyToken
            {
                Server = request.Server ?? host,
                Host = host,
                Port = port,
                Username = request.Username,
                Password = request.Password,
                Dir = directory,
                UseSsl = request.Ssl,
                Passive = request.Passive,
                IgnoreCertErrors = request.IgnoreCertErrors,
                TimeoutSeconds = timeoutSeconds
            };

            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(payload, jsonOptions);
            string token = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            var responsePayload = JsonSerializer.Deserialize<JsonElement>(json);

            return Ok(new
            {
                token,
                authorizationHeader = $"Authorization: Bearer {token}",
                payload = responsePayload
            });
        }

        public sealed class GenerateTokenRequest
        {
            [FromQuery(Name = "server")]
            public string? Server { get; set; }

            [FromQuery(Name = "host")]
            public string? Host { get; set; }

            [FromQuery(Name = "port")]
            public int? Port { get; set; }

            [FromQuery(Name = "username")]
            public string? Username { get; set; }

            [FromQuery(Name = "password")]
            public string? Password { get; set; }

            [FromQuery(Name = "dir")]
            public string? Dir { get; set; }

            [FromQuery(Name = "ssl")]
            public bool? Ssl { get; set; }

            [FromQuery(Name = "passive")]
            public bool? Passive { get; set; }

            [FromQuery(Name = "ignoreCertErrors")]
            public bool? IgnoreCertErrors { get; set; }

            [FromQuery(Name = "timeoutSeconds")]
            public int? TimeoutSeconds { get; set; }
        }
    }
}
