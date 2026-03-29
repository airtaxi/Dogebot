using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace KakaoBotAT.Server.Controllers;

[ApiController]
[Route("api/kakao")]
public class KakaoController(IKakaoService kakaoService, ILogger<KakaoController> logger) : ControllerBase
{
    /// <summary>
    /// Receives notification messages from the MAUI client and returns an immediate response.
    /// POST /api/kakao/notify
    /// </summary>
    [HttpPost("notify")]
    [ProducesResponseType(typeof(ServerResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Notify([FromBody] ServerNotification notification)
    {
        if (notification == null || notification.Data == null)
        {
            return BadRequest(new ServerResponse { Action = "error", Message = "Invalid notification data." });
        }

        var response = await kakaoService.HandleNotificationAsync(notification);
        return Ok(response);
    }

    /// <summary>
    /// Responds to polling requests from the MAUI client by delivering server queued commands.
    /// The client passes available room IDs (rooms with active reply actions) as a query parameter.
    /// GET /api/kakao/command?availableRooms=roomId1,roomId2,...
    /// </summary>
    [HttpGet("command")]
    [ProducesResponseType(typeof(ServerResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Command([FromQuery] string? availableRooms)
    {
        var roomIds = string.IsNullOrEmpty(availableRooms)
            ? []
            : availableRooms.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (roomIds.Length > 0 && logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("[COMMAND] Polling with {Count} available rooms", roomIds.Length);

        var command = await kakaoService.GetPendingCommandAsync(roomIds);
        return Ok(command);
    }
}