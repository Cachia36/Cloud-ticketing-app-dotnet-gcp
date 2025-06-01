using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using cloud_ticket_app.Models;
using StackExchange.Redis;


namespace cloud_ticket_app.Controllers;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _redis;

    public HomeController(
        ILogger<HomeController> logger,
        IConfiguration configuration,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _configuration = configuration;
        _redis = redis; 
    }
    public async Task<IActionResult> Index()
    {
        return View();
    }
    
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}