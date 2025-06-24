using McpDotNet.Server;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace mcpwebapi.Controllers
{
    [McpToolType]
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        [McpTool("GetWeather"), Description("获取天气情况")]
        public IEnumerable<WeatherForecast> Get([Description("城市名称")]string city)
        {
            return Enumerable.Range(1, 1).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)],
                City = city
            })
            .ToArray();
        }
    }
}
