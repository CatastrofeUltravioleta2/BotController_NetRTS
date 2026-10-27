using Microsoft.AspNetCore.Mvc;

namespace BadgerBot.Api.Controllers;

[ApiController]
[Route("api/bot")]
public class BotController(BotService bot) : ControllerBase
{
    [HttpGet("status")]
    public BotStatus Status() => bot.GetStatus();

    [HttpPost("start")]
    public IActionResult Start() =>
        bot.Start() ? Accepted(bot.GetStatus()) : Conflict(new { message = "The bot is already running." });

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await bot.StopAsync();
        return Accepted(bot.GetStatus());
    }
}