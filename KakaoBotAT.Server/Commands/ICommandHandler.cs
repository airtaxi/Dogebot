using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Interface for command handlers.
/// 
/// ⚠️ IMPORTANT: When implementing a new command handler:
/// 1. Implement this interface in your new command handler class
/// 2. Register it in Program.cs: builder.Services.AddSingleton&lt;ICommandHandler, YourCommandHandler&gt;();
/// 3. Update HelpCommandHandler.cs to include your new command in the help message
///    - Add your command under the appropriate category (🎮 게임 & 랜덤, 🎭 재미, 📊 통계, or ℹ️ 기타)
///    - Format: "• [command] - [description]"
/// 
/// Example:
/// If you add a "!날씨" command, update HelpCommandHandler.cs:
/// "ℹ️ 기타\n" +
/// "• !날씨 - 현재 날씨 확인\n" +
/// "• !도움말 / !help - 이 메시지"
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Gets the command that this handler processes (e.g., "!핑").
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Determines whether this handler can process the given message content.
    /// </summary>
    bool CanHandle(string content);

    /// <summary>
    /// Handles the command and returns a response.
    /// </summary>
    Task<ServerResponse> HandleAsync(KakaoMessageData data);
}
