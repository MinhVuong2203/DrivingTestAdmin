using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AdMobConfigController : ControllerBase
{
    private readonly IConfiguration _config;

    public AdMobConfigController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>Lấy AdMob config cho Flutter app</summary>
    [HttpGet]
    public IActionResult GetConfig()
    {
        var config = new AdMobConfig
        {
            AppId = _config["AdMob:AppId"] ?? "",
            BannerId = _config["AdMob:BannerId"] ?? "",
            InterstitialId = _config["AdMob:InterstitialId"] ?? "",
            RewardedId = _config["AdMob:RewardedId"] ?? "",
        };
        return Ok(config);
    }
}