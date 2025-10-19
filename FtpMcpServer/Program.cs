using FtpMcpServer.Auth;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Add HttpContext accessor for scoped services
builder.Services.AddHttpContextAccessor();

// Register per-request FtpDefaults sourced from the authenticated user's claims
builder.Services.AddScoped<FtpMcpServer.FtpDefaults>(sp =>
{
    var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    var user = http?.User;
    return FtpMcpServer.FtpDefaults.FromClaims(user);
});

// Add authentication/authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
}).AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();

// Build the MCP server with HTTP transport, our FTP Tools and Resource
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<FtpMcpServer.Tools.FtpTools>()  // expose FTP tools
    .WithResources<FtpMcpServer.Resources.FtpResourceType>(); // expose ftp_file resource

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

// Middleware
app.UseAuthentication();
app.UseAuthorization();

// Map MCP HTTP endpoints under /mcp and require authentication
app.MapMcp("/mcp").RequireAuthorization();

app.MapControllers();

app.Run();