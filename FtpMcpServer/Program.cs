using System.Threading.Tasks;
using FtpMcpServer;
using FtpMcpServer.Auth;
using FtpMcpServer.Resources;
using FtpMcpServer.Services;
using FtpMcpServer.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

var builder = WebApplication.CreateBuilder(args);

// Core ASP.NET services
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<FileExtensionContentTypeProvider>();

// Authentication: custom API key / bearer handler
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName,
        _ => { });

// Authorization
builder.Services.AddAuthorization();

// Per-request FTP defaults derived from authenticated user claims
builder.Services.AddScoped<FtpDefaults>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var user = http?.User;
    return FtpDefaults.FromClaims(user);
});

// FTP service and tool dependencies
builder.Services.AddSingleton<IFluentFtpService, FluentFtpService>();
builder.Services.AddSingleton<FtpTools>();

// MCP server configuration (HTTP URL transport)
builder.Services
    .AddMcpServer()
    // Keep HTTP transport so the Playground can connect via URL.
    .WithHttpTransport(options =>
    {
        // You can tweak options here (e.g., idle timeouts) if desired.
        // options.IdleTimeout = Timeout.InfiniteTimeSpan;
        options.IdleTimeout = Timeout.InfiniteTimeSpan;
    })
    // Optional but recommended: enables [Authorize]/[AllowAnonymous] if you
    // later decorate tools/resources with attributes.
    .AddAuthorizationFilters()
    // Your tools and resources
    .WithTools<FtpTools>()
    .WithResources<FluentFtpResourceType>()
    // Optional: handle logging/setLevel so clients (like Playground) can adjust verbosity cleanly.
    .WithSetLoggingLevelHandler(async (request, ct) =>
    {
        // The server tracks the current logging level internally; returning an EmptyResult is sufficient.
        // If you want to actually change Microsoft.Extensions.Logging levels dynamically, you can wire that up here.
        await Task.CompletedTask.ConfigureAwait(false);
        return new EmptyResult();
    });

var app = builder.Build();

// Standard middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map MCP endpoints under /mcp and require auth for all of them.
// This maps the Streamable HTTP endpoint (/mcp) and also SSE compatibility endpoints (/mcp/sse and /mcp/message).
app.MapMcp("/mcp").RequireAuthorization();

// Any additional controllers (e.g., HealthController) remain available
app.MapControllers();

app.Run();