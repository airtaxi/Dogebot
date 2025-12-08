# KakaoBotAT

A KakaoTalk bot system built with .NET 10 that uses Android's NotificationListenerService to intercept and respond to KakaoTalk messages.

## Author

**airtaxi**

## License

See the [LICENSE](LICENSE) file for details.

## Overview

KakaoBotAT is a distributed system consisting of three main components:

- **KakaoBotAT.MobileClient**: A .NET MAUI Android application that listens to KakaoTalk notifications and communicates with the server
- **KakaoBotAT.Server**: An ASP.NET Core REST API server that processes messages with extensible command handlers and MongoDB-based statistics
- **KakaoBotAT.Commons**: Shared data models and contracts used by both client and server

## Architecture

```
┌─────────────────────┐         ┌─────────────────────┐
│  KakaoTalk App      │         │  MAUI Client        │
│  (Android)          │         │  (Android)          │
└──────────┬──────────┘         └──────────┬──────────┘
           │                               │
           │ Notification                  │
           └──────────────────────────────►│
                                           │
                                           │ POST /api/kakao/notify
                                           ▼
                              ┌────────────────────────┐
                              │  ASP.NET Core Server   │
                              │  • Command Handlers    │
                              │  • MongoDB Stats       │
                              └────────────────────────┘
                                           │
                                           │ Command Response
                                           ▼
           ┌───────────────────────────────┘
           │ GET /api/kakao/command (Polling)
           │
           ▼
┌──────────────────────┐
│  MAUI Client         │
│  Sends Reply/Read    │
└──────────────────────┘
```

## Features

### Mobile Client
- **Notification Listener**: Intercepts KakaoTalk notifications using Android's NotificationListenerService
- **Message Processing**: Extracts message content, sender information, and room details
- **Action Execution**: Can send replies and mark messages as read through notification actions
- **Server Communication**: Sends notifications to server and polls for commands
- **Battery Optimization**: Implements WakeLock to ensure continuous operation
- **Permission Management**: Guides users through notification access and battery optimization settings

### Server
- **REST API**: Provides endpoints for receiving notifications and delivering commands
- **Command Handler Pattern**: Extensible architecture for adding new bot commands
- **Built-in Commands**: 
  - `!핑` - Responds with `퐁` (ping/pong)
  - `!순위` - Shows chat activity ranking
  - `!내순위` - Shows user's personal ranking
  - `!등수 [순위]` - Shows specific rank information
- **MongoDB Integration**: Stores chat statistics and message history
- **Statistics Tracking**: Records message counts per user and room
- **Logging**: Built-in logging for debugging and monitoring

## Technology Stack

- **.NET 10**: Latest .NET framework
- **C# 14.0**: Latest C# language features
- **.NET MAUI**: Cross-platform UI framework (Android target)
- **ASP.NET Core**: Web API framework
- **MongoDB**: NoSQL database for statistics and chat history
- **CommunityToolkit.Mvvm**: MVVM helpers and patterns
- **System.Text.Json**: JSON serialization

## Prerequisites

### For Development
- Visual Studio 2022 or later
- .NET 10 SDK
- Android SDK (API Level 21+)
- Android device or emulator with KakaoTalk installed
- MongoDB instance (optional, for statistics features)

### For Deployment
- **Mobile Client**: Android device with KakaoTalk installed
- **Server**: Any platform supporting .NET 10 (Windows, Linux, macOS)
- **Database**: MongoDB instance (optional)

## Setup

### 1. Clone the Repository
```bash
git clone https://github.com/airtaxi/KakaoBotAT.git
cd KakaoBotAT
```

### 2. Configure Server Endpoint
Edit `KakaoBotAT.MobileClient\Constants.cs` and update the server URL:
```csharp
internal const string ServerEndpointUrl = "https://your-server-url.com/api/kakao";
```

### 3. Configure MongoDB (Optional)
If you want to use statistics features, configure MongoDB connection in `KakaoBotAT.Server\appsettings.json`:
```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "KakaoBotAT"
  }
}
```

### 4. Build the Solution
```bash
dotnet build
```

### 5. Run the Server
```bash
cd KakaoBotAT.Server
dotnet run
```

### 6. Deploy Mobile Client
Deploy the `KakaoBotAT.MobileClient` project to your Android device through Visual Studio.

## Usage

### Mobile Client Setup

1. **Grant Notification Access**
   - Open the app
   - Tap "Open Notification Listener Settings"
   - Enable notification access for KakaoBotAT

2. **Disable Battery Optimization**
   - Tap "Request Battery Optimization Exemption"
   - Allow the app to run in the background

3. **Configure Server**
   - Enter your server endpoint URL
   - Tap "Update Status" to verify settings

4. **Start the Bot**
   - Tap "Start Bot"
   - The app will now listen for KakaoTalk messages and communicate with the server

### Adding Bot Commands

Create a new command handler by implementing the `ICommandHandler` interface:

```csharp
using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class HelloCommandHandler : ICommandHandler
{
    public string Command => "!hello";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        return Task.FromResult(new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = "Hello! How can I help you?"
        });
    }
}
```

Then register it in `Program.cs`:
```csharp
builder.Services.AddSingleton<ICommandHandler, HelloCommandHandler>();
```

## API Endpoints

### POST /api/kakao/notify
Receives notification messages from the MAUI client and returns immediate command response.

**Request Body:**
```json
{
  "event": "message",
  "data": {
    "roomName": "Chat Room",
    "roomId": "room_id_hash",
    "senderName": "Sender Name",
    "senderHash": "sender_hash",
    "content": "Message content",
    "logId": "123456",
    "isGroupChat": false,
    "time": 1234567890
  }
}
```

**Response:**
```json
{
  "action": "send_text",
  "roomId": "room_id_hash",
  "message": "Bot response"
}
```

### GET /api/kakao/command
Polling endpoint for retrieving queued commands (currently returns empty response).

**Response:**
```json
{
  "action": "",
  "roomId": "",
  "message": ""
}
```

## Project Structure

```
KakaoBotAT/
├── KakaoBotAT.Commons/
│   ├── KakaoMessageData.cs        # Message data model
│   ├── ServerNotification.cs      # Notification request model
│   └── ServerResponse.cs          # Server response model
├── KakaoBotAT.MobileClient/
│   ├── Platforms/
│   │   └── Android/
│   │       ├── KakaoNotificationListener.cs  # Notification interceptor
│   │       └── KakaoBotService.cs            # Android-specific services
│   ├── ViewModels/
│   │   └── MainViewModel.cs       # Main UI logic
│   ├── MainPage.xaml              # Main UI
│   ├── MainPage.xaml.cs           # UI code-behind with converters
│   ├── Constants.cs               # Configuration constants
│   ├── IKakaoBotService.cs        # Service interface
│   ├── MauiProgram.cs             # App configuration
│   └── App.xaml                   # Application resources
├── KakaoBotAT.Server/
│   ├── Commands/
│   │   ├── ICommandHandler.cs            # Command handler interface
│   │   ├── CommandHandlerFactory.cs      # Handler factory
│   │   ├── PingCommandHandler.cs         # !핑 command
│   │   ├── RankingCommandHandler.cs      # !순위 command
│   │   ├── MyRankingCommandHandler.cs    # !내순위 command
│   │   └── RankCommandHandler.cs         # !등수 command
│   ├── Controllers/
│   │   └── KakaoController.cs     # API endpoints
│   ├── Services/
│   │   ├── IKakaoService.cs              # Service interface
│   │   ├── KakaoService.cs               # Bot logic implementation
│   │   ├── IMongoDbService.cs            # MongoDB interface
│   │   ├── MongoDbService.cs             # MongoDB implementation
│   │   ├── IChatStatisticsService.cs     # Statistics interface
│   │   └── ChatStatisticsService.cs      # Statistics implementation
│   └── Program.cs                 # Server entry point
└── README.md
```

## Permissions Required

### Android Permissions
- `INTERNET`: Network communication
- `ACCESS_NETWORK_STATE`: Check network connectivity
- `BIND_NOTIFICATION_LISTENER_SERVICE`: Listen to notifications
- `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`: Background operation
- `WAKE_LOCK`: Keep CPU awake for continuous operation

## Troubleshooting

### Bot Not Receiving Messages
1. Verify notification access is granted in Android settings
2. Check battery optimization is disabled for the app
3. Ensure KakaoTalk is installed and logged in
4. Verify server endpoint URL is correct in Constants.cs
5. Check that the bot is started (green button shows "Stop Bot")

### Server Connection Issues
1. Check server is running and accessible
2. Verify firewall settings allow incoming connections
3. Check server endpoint URL matches in Constants.cs
4. Review network connectivity on mobile device
5. Check server logs for error messages

### Replies Not Sending
1. Reply actions expire when notification is dismissed
2. Ensure KakaoTalk notification is still visible in notification shade
3. Check logcat for detailed error messages
4. Verify notification actions are properly extracted

### Statistics Not Working
1. Verify MongoDB is running and accessible
2. Check MongoDB connection string in appsettings.json
3. Review server logs for database connection errors

## Built-in Commands

| Command | Description | Example Response |
|---------|-------------|------------------|
| `!핑` | Ping command | `퐁` |
| `!순위` | Show chat activity ranking | Top 10 users by message count |
| `!내순위` | Show your personal ranking | Your rank and message count |
| `!등수 [N]` | Show specific rank | User at rank N |

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## Disclaimer

This project is for educational purposes. Make sure to comply with KakaoTalk's Terms of Service when using this bot.
