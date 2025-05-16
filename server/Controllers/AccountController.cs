using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace fairwayfinder.Controllers
{
  [Authorize]
  [ApiController]
  [Route("[controller]")]
  public class AccountController : ControllerBase
  {
    private readonly AccountService _accountService;
    private readonly Auth0Provider _auth0Provider;
    private readonly HttpClient _httpClient;

    // Inject services via constructor
    public AccountController(AccountService accountService, Auth0Provider auth0Provider, HttpClient httpClient)
    {
      _accountService = accountService;
      _auth0Provider = auth0Provider;
      _httpClient = httpClient;
    }

    [HttpGet]
    public async Task<ActionResult<Account>> Get()
    {
      try
      {
        // Retrieve user information using Auth0 provider
        Account userInfo = await _auth0Provider.GetUserInfoAsync<Account>(HttpContext);
        // Return the account information from the service (create or fetch the account)
        return Ok(_accountService.GetOrCreateAccount(userInfo));
      }
      catch (Exception e)
      {
        // Return a BadRequest in case of errors
        return BadRequest($"Error retrieving account: {e.Message}");
      }
    }

    [HttpGet("tee-times")]
    public async Task<IActionResult> GetForeupTeeTimes()
    {
      var url = "https://foreupsoftware.com/index.php/api/booking/times?time=all&date=05-18-2025&holes=all&players=0&booking_class=14951&schedule_id=1801&schedule_ids%5B%5D=1801&specials_only=0&api_key=no_limits";

      try
      {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("accept", "application/json, text/javascript, */*; q=0.01");
        request.Headers.Add("api-key", "no_limits");
        request.Headers.Referrer = new Uri("https://foreupsoftware.com/index.php/booking/20879/5971");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var rawTeeTimes = JsonSerializer.Deserialize<List<JsonElement>>(json);

        var teeTimes = rawTeeTimes
            .Where(t =>
                t.TryGetProperty("time", out _) &&
                t.TryGetProperty("course_name", out _) &&
                t.TryGetProperty("available_spots", out _) &&
                t.TryGetProperty("green_fee", out _))
            .Select(t => new TeeTime
            {
              Time = t.GetProperty("time").GetString(),
              CourseName = t.GetProperty("course_name").GetString(),
              AvailableSpots = t.GetProperty("available_spots").GetInt32(),
              GreenFee = t.GetProperty("green_fee").GetDecimal()
            })
            .ToList();

        return Ok(teeTimes);
      }
      catch (Exception ex)
      {
        return StatusCode(500, $"ForeUp error: {ex.Message}");
      }
    }


    [HttpGet("ridgecrest")]
    public async Task<IActionResult> GetGolfRevTeeTimes()
    {
      var url = "https://www.golfrev.com/go/tee_times/teetime_table_html.asp?c=141&s=5%2F17%2F2025&h=284&specials=&reset=yes&snapshot=no";

      try
      {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "text/html, */*; q=0.01");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("User-Agent", "Mozilla/5.0");
        request.Headers.Referrer = new Uri("https://www.golfrev.com/go/tee_times/?htc=358&courseid=3713&r=1");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card-body')]");
        if (cards == null)
          return Ok(new List<TeeTime>());

        var teeTimes = new List<TeeTime>();

        foreach (var card in cards)
        {
          var time = card.SelectSingleNode(".//h5[contains(@class, 'card-title')]")?.InnerText.Trim();
          var course = card.SelectSingleNode(".//p[contains(@class, 'card-text text-secondary')]")?.InnerText.Trim();
          var playersText = card.SelectSingleNode(".//p[contains(text(), 'players')]")?.InnerText.Trim();
          var priceText = card.SelectSingleNode(".//p[contains(@class, 'cust-card-trim')]")?.InnerText.Trim();

          if (string.IsNullOrEmpty(time) || string.IsNullOrEmpty(course) || string.IsNullOrEmpty(playersText) || string.IsNullOrEmpty(priceText))
            continue;

          if (!int.TryParse(Regex.Match(playersText, @"(\d+)").Groups[1].Value, out var spots)) continue;
          if (!decimal.TryParse(Regex.Match(priceText, @"\$(\d+(\.\d{1,2})?)").Groups[1].Value, out var fee)) continue;

          teeTimes.Add(new TeeTime
          {
            Time = time,
            CourseName = course,
            AvailableSpots = spots,
            GreenFee = fee
          });
        }

        return Ok(teeTimes);
      }
      catch (Exception ex)
      {
        return StatusCode(500, $"GolfRev error: {ex.Message}");
      }
    }




    [HttpGet("ridgecrest-raw")]
    public async Task<IActionResult> GetRidgeCrestRaw()
    {
      string url = "https://www.golfrev.com/go/tee_times/teetime_table_html.asp?c=3713&s=5%2F15%2F2025&h=358&specials=&reset=yes&snapshot=no";

      try
      {
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
          var errorContent = await response.Content.ReadAsStringAsync();
          Console.WriteLine($"Error: {response.StatusCode}, Content: {errorContent}");
          return StatusCode(500, $"HTTP request failed with status code {response.StatusCode}");
        }

        var html = await response.Content.ReadAsStringAsync();

        return Content(html, "text/html");
      }
      catch (HttpRequestException ex)
      {
        // Log and handle HTTP errors
        Console.WriteLine($"HttpRequestException: {ex.Message}");
        return StatusCode(500, $"HTTP request error: {ex.Message}");
      }
      catch (Exception ex)
      {
        // Log unexpected errors
        Console.WriteLine($"Unexpected error: {ex.Message}, StackTrace: {ex.StackTrace}");
        return StatusCode(500, $"Unexpected error: {ex.Message}");
      }
    }
  }
}
