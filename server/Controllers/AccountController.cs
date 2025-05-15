using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    public async Task<IActionResult> GetTeeTimes()
    {
      var url = "https://foreupsoftware.com/index.php/api/booking/times?time=all&date=05-14-2025&holes=all&players=0&booking_class=6524&schedule_id=5971&schedule_ids%5B%5D=5971&specials_only=0&api_key=no_limits";

      try
      {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("accept", "application/json, text/javascript, */*; q=0.01");
        request.Headers.Add("accept-language", "en-US,en;q=0.9");
        request.Headers.Add("api-key", "no_limits");
        request.Headers.Add("cache-control", "no-cache");
        request.Headers.Add("pragma", "no-cache");
        request.Headers.Add("priority", "u=1, i");
        request.Headers.Add("sec-fetch-dest", "empty");
        request.Headers.Add("sec-fetch-mode", "cors");
        request.Headers.Add("sec-fetch-site", "same-origin");
        request.Headers.Add("x-fu-golfer-location", "foreup");
        request.Headers.Add("x-requested-with", "XMLHttpRequest");
        request.Headers.Referrer = new Uri("https://foreupsoftware.com/index.php/booking/20879/5971");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return Content(content, "application/json");
      }
      catch (HttpRequestException ex)
      {
        return StatusCode(500, $"HTTP request error: {ex.Message}");
      }
      catch (Exception ex)
      {
        return StatusCode(500, $"Unexpected error: {ex.Message}");
      }
    }

    [HttpGet("ridgecrest")]
    public async Task<IActionResult> GetRidgeCrest()
    {
      var url = "https://www.golfrev.com/go/tee_times/teetime_table_html.asp?c=141&s=5%2F15%2F2025&h=284&specials=&reset=yes&snapshot=no";

      try
      {
        // Prepare the HTTP request with browser-like headers
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "text/html, */*; q=0.01");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Cache-Control", "no-cache");
        request.Headers.Add("Pragma", "no-cache");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        request.Headers.Referrer = new Uri("https://www.golfrev.com/go/tee_times/?htc=358&courseid=3713&r=1");

        // Send the request
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
          var errorContent = await response.Content.ReadAsStringAsync();
          return StatusCode((int)response.StatusCode, $"Request failed with status {response.StatusCode}: {errorContent}");
        }

        // Parse the HTML
        var html = await response.Content.ReadAsStringAsync();
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        var cardBodies = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'card-body')]");
        if (cardBodies == null || !cardBodies.Any())
        {
          return Ok(new { message = "HTML fetched, but no tee time cards found." });
        }

        var teeTimes = new List<object>();

        foreach (var card in cardBodies)
        {
          var time = card.SelectSingleNode(".//h5[contains(@class, 'card-title')]")?.InnerText.Trim();
          var course = card.SelectSingleNode(".//p[contains(@class, 'card-text text-secondary')]")?.InnerText.Trim();
          var players = card.SelectSingleNode(".//p[contains(text(), 'players')]")?.InnerText.Trim();
          var price = card.SelectSingleNode(".//p[contains(@class, 'cust-card-trim')]")?.InnerText.Trim() ?? "N/A";

          if (!string.IsNullOrEmpty(time) && !string.IsNullOrEmpty(course) && !string.IsNullOrEmpty(players))
          {
            teeTimes.Add(new
            {
              Time = time,
              Course = course,
              Players = players,
              Price = price
            });
          }
        }

        if (!teeTimes.Any())
        {
          return Ok(new { message = "HTML parsed, but no complete tee time data found." });
        }

        return Ok(teeTimes);
      }
      catch (HttpRequestException ex)
      {
        return StatusCode(500, $"Request error: {ex.Message}");
      }
      catch (Exception ex)
      {
        return StatusCode(500, $"Unexpected error: {ex.Message}");
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
