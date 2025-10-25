from openai import OpenAI
client = OpenAI()

response = client.responses.create(
  model="gpt-5",
  input=[
    {
      "role": "user",
      "content": [
        {
          "type": "input_text",
          "text": "Please test all mcp tools"
        }
      ]
    }
  ],
  text={
    "format": {
      "type": "text"
    },
    "verbosity": "medium"
  },
  reasoning={
    "effort": "medium"
  },
  tools=[
    {
      "type": "mcp",
      "server_label": "ftp",
      "server_url": "https://ftpmcp.azurewebsites.net/mcp",
      "authorization": "eyJzZXJ2ZXIiOiJmdHAud29ybGRvZnRveWdlcnMuY29tIiwiaG9zdCI6ImZ0cC53b3JsZG9mdG95Z2Vycy5jb20iLCJwb3J0IjoyMSwidXNlcm5hbWUiOiJhaWZ0cEBvcGNjb25saW5lLmNvbSIsInBhc3N3b3JkIjoiU2lTS3kwdGJYZnM5dExWNCIsImRpciI6Ii8iLCJzc2wiOmZhbHNlLCJwYXNzaXZlIjp0cnVlLCJpZ25vcmVDZXJ0RXJyb3JzIjp0cnVlLCJ0aW1lb3V0U2Vjb25kcyI6NjB9",
      "allowed_tools": [
        "ftp_makeDirectory",
        "ftp_getModifiedTime",
        "ftp_removeDirectory",
        "ftp_uploadFile",
        "ftp_getFileSize",
        "ftp_writeFile",
        "ftp_deleteFile",
        "ftp_retreiveFile",
        "ftp_rename",
        "ftp_listDirectory"
      ],
      "require_approval": "never"
    }
  ],
  store=True,
  include=[
    "reasoning.encrypted_content",
    "web_search_call.action.sources"
  ]
)

print(response)