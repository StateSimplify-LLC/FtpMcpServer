using System;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FtpMcpServer.Controllers
{
    [ApiController]
    [Route("")]
    public class HealthController : ControllerBase
    {
        private static readonly DateTimeOffset Started = DateTimeOffset.UtcNow;

        // Root - simple status (replaces the current MapGet("/") in Program.cs)
        [HttpGet("")]
        [AllowAnonymous]
        public IActionResult Root()
        {
            return Ok(new
            {
                name = "FtpMcpServer",
                status = "ok",
                mcp = "/mcp",
                uptimeSeconds = (long)(DateTimeOffset.UtcNow - Started).TotalSeconds,
                auth = "Provide base64-encoded JSON token via Authorization: Bearer or X-Api-Key"
            });
        }

        // Liveness probes
        [HttpGet("health")]
        [HttpGet("healthz")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "ok",
                timeUtc = DateTimeOffset.UtcNow
            });
        }

        // For HEAD-based health checks
        [HttpHead("health")]
        [HttpHead("healthz")]
        [AllowAnonymous]
        public IActionResult HealthHead() => Ok();

        // Info endpoint with basic runtime details
        [HttpGet("info")]
        [AllowAnonymous]
        public IActionResult Info()
        {
            var asm = typeof(Program).Assembly.GetName();
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

            return Ok(new
            {
                name = asm.Name,
                version = asm.Version?.ToString(),
                environment = env,
                framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                runtime = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,
                processId = Environment.ProcessId,
                startedUtc = Started,
                uptimeSeconds = (long)(DateTimeOffset.UtcNow - Started).TotalSeconds,
                endpoints = new[] { "/", "/health", "/healthz", "/info", "/mcp" },
                auth = "Provide base64-encoded JSON token via Authorization: Bearer or X-Api-Key"
            });
        }
    }
}