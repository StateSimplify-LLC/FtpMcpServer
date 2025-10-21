# FtpMcpServer — an MCP ↔ FTP proxy in C# (.NET 8)

FtpMcpServer lets any Model Context Protocol (MCP) client talk to plain FTP/FTPS servers through a lightweight, stateless proxy written in C#. It exposes safe, well-described MCP tools and a resource type that map directly to FTP operations using FluentFTP.

## Highlights
- Pure .NET 8 with the official MCP C# SDK (HTTP transport under `/mcp`).
- Stateless, per-request connection: clients pass an API key (a base64-encoded JSON payload) that contains the FTP host, credentials, and options. No server-side credential storage.
- Read/write support: list, download, upload, write text, delete, mkdir/rmdir, rename, file size, modified time.
- Resource type for streaming files back as MCP resources with correct MIME type.
- Works with OpenAI-compatible MCP clients, including the OpenAI Playground, via the HTTP endpoint.

## How it works
- Clients connect to the MCP endpoint over HTTP (`/mcp`).
- Each request includes an `Authorization: Bearer <token>` or `X-Api-Key: <token>`, where `<token>` is a base64-encoded JSON object describing the target FTP server and connection options.
- The server derives per-request `FtpDefaults` from the claims in that token (host, port, username/password, dir, SSL, passive mode, timeouts, etc.).
- Tools execute with FluentFTP and return structured results or embedded resources.

```
ASCII overview
client (MCP) ──HTTP──> /mcp (auth via Bearer/X-Api-Key)
   │                                 │
   │                          build FtpDefaults (from token claims)
   │                                 │
   └────────── tools/resources <─────┘
                                FluentFTP → FTP/FTPS server
```

## Quick start
**Prerequisites**
- .NET SDK 8.0+
- An FTP/FTPS server reachable from the machine running FtpMcpServer

**Build and run**
- Clone and restore:
  - `git clone <your-repo-url>`
  - `dotnet restore`
- Run locally on a predictable port (example 8787):
  - `dotnet run --urls "http://localhost:8787"`
- Verify the server is up (no auth required for health endpoints) by visiting:
  - `http://localhost:8787/health`
  - `http://localhost:8787/info`

MCP endpoints are under `/mcp` and require auth.

## Authentication and token format
Provide credentials and connection settings in a base64-encoded JSON token via the `Authorization` or `X-Api-Key` header. The payload supports:

**Required**
- `server` or `host`: FTP hostname/IP  
- `port`: number (default 21 if omitted)  
- `username`: string  
- `password`: string  
- `dir`: starting directory, e.g., `"/"`  

**Optional**
- `ssl`: boolean (use FTPS)  
- `passive`: boolean (default `true`)  
- `ignoreCertErrors`: boolean (FTPS validation)  
- `timeoutSeconds`: number  

**Example token JSON**
```json
{
  "server": "ftp.example.com",
  "port": 21,
  "username": "demo",
  "password": "s3cr3t",
  "dir": "/pub",
  "ssl": false,
  "passive": true,
  "ignoreCertErrors": false,
  "timeoutSeconds": 30
}
```

Encode this JSON as UTF-8, then base64, and pass it as the bearer token. (How you encode it is up to your client environment.)

### Generate a token with the built-in endpoint
If your build includes the `TokenController` (`GET /token`), you can generate a ready-to-use token directly from the running server (no local scripting required):

**1) Start the server**  
Ensure FtpMcpServer is running, e.g. at `http://localhost:8787`.

**2) Open the token URL**  
Visit this in a browser or HTTP client, replacing values with your FTP details:

```
http://localhost:8787/token
  ?server=ftp.example.com
  &username=demo
  &password=s3cr3t
  &dir=/pub
  &port=21
  &ssl=false
  &passive=true
  &ignoreCertErrors=false
  &timeoutSeconds=30
```

> Notes
> - You may use either `server` or `host`. If both are provided, `host` takes precedence for connection and `server` is echoed in the payload.
> - Defaults: `port=21`, `dir=/`. Optional booleans and `timeoutSeconds` may be omitted.
> - Validation: missing `server/host`, `username`, or `password` returns 400. `port` must be > 0; `timeoutSeconds` (when provided) must be > 0.

**3) Copy the result**  
The endpoint returns JSON like:

```json
{
  "token": "<BASE64_STRING>",
  "authorizationHeader": "Authorization: Bearer <BASE64_STRING>",
  "payload": {
    "server": "ftp.example.com",
    "host": "ftp.example.com",
    "port": 21,
    "username": "demo",
    "dir": "/pub",
    "useSsl": false,
    "passive": true,
    "ignoreCertErrors": false,
    "timeoutSeconds": 30
  }
}
```

Use either the raw `"token"` or copy the `"authorizationHeader"` line as-is into your client’s headers.

**4) Use the token with MCP**  
When connecting to `/mcp`, include:
```
Authorization: Bearer <BASE64_STRING>
```
(or `X-Api-Key: <BASE64_STRING>`)

> Tip: If your password contains characters that have special meaning in URLs (`&`, `+`, `#`, etc.), URL-encode it in the query string, or use an HTTP client that handles this automatically via query parameters.

## Connect from OpenAI API & Playground
- **Endpoint:** `http://<host>:<port>/mcp`  
- **Headers:** Include `Authorization: Bearer <YOUR_BASE64_TOKEN>` (or `X-Api-Key`).  
- **Playground:** Configure your MCP connection to point at the endpoint above and supply the header with your token.

## Available tools and resources
**Tools (class: `FtpTools`)**
- `ftp_listDirectory(path?)`: List entries with `name`, `isDirectory`, `size`, `modified`, `permissions`.
- `ftp_downloadFile(path)`: Return file as an embedded resource (base64) with MIME detection.
- `ftp_uploadFile(path, dataBase64)`: Upload base64 content to the path (creates directories as needed).
- `ftp_writeFile(path, content, encoding?)`: Write plain text using the specified encoding (UTF-8 by default).
- `ftp_deleteFile(path)`: Delete a file.
- `ftp_makeDirectory(path)`: Create a directory.
- `ftp_removeDirectory(path)`: Remove an empty directory.
- `ftp_rename(path, newName)`: Rename a file or directory within its parent.
- `ftp_getFileSize(path)`: Return size in bytes.
- `ftp_getModifiedTime(path)`: Return last modified time.

**Resource type (class: `FluentFtpResourceType`)**
- `ftp_file`: Read a remote file and return a Blob resource with MIME type inferred from the path.
