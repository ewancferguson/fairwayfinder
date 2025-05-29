using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using fairwayfinder.Models;
using fairwayfinder.Repositories;
using Microsoft.Playwright;

namespace fairwayfinder.Services;

public class GolfCourseService
{
  private readonly GolfCourseRepository _repository;
  private readonly HttpClient _httpClient;

  public GolfCourseService(GolfCourseRepository repository, HttpClient httpClient)
  {
    _repository = repository;
    _httpClient = httpClient;
  }

  public List<GolfCourse> GetGolfCourses()
  {
    return _repository.GetGolfCourses();
  }

  public GolfCourse GetGolfCourseById(int golfCourseId)
  {
    var golfCourse = _repository.GetGolfCourseById(golfCourseId);
    if (golfCourse == null) throw new Exception("Golf Course does not exist");
    return golfCourse;
  }

  public async Task<List<TeeTime>> GetTeeTimesAsync(int courseId)
  {
    var course = GetGolfCourseById(courseId);
    if (course == null) throw new Exception("Golf Course does not exist");

    return course.BookingSoftware switch
    {
      "golfrev" => await GolfRevTeeTimesWithPlaywright(course),
      "foreup" => await ForeupTeeTimes(course),
      _ => throw new Exception("Unsupported booking software")
    };
  }

  private async Task<List<TeeTime>> ForeupTeeTimes(GolfCourse course)
  {
    await InitializeForeUpSessionAsync(course);

    string today = DateTime.Now.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
    string url = course.FetchUrl.Replace("{DATE}", today);

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("accept", "application/json, text/javascript, */*; q=0.01");
    request.Headers.Add("accept-language", "en-US,en;q=0.9");
    request.Headers.Add("cache-control", "no-cache");
    request.Headers.Add("pragma", "no-cache");
    request.Headers.Add("sec-fetch-dest", "empty");
    request.Headers.Add("sec-fetch-mode", "cors");
    request.Headers.Add("sec-fetch-site", "same-origin");
    request.Headers.Add("x-fu-golfer-location", "foreup");
    request.Headers.Add("x-requested-with", "XMLHttpRequest");
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
          Time = DateTime.ParseExact(
                    t.GetProperty("time").GetString(),
                    "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture)
                .ToString("h:mm tt", CultureInfo.InvariantCulture),
          CourseName = t.GetProperty("course_name").GetString(),
          AvailableSpots = t.GetProperty("available_spots").GetInt32(),
          GreenFee = t.GetProperty("green_fee").GetDecimal()
        })
        .ToList();

    return teeTimes;
  }

  private async Task<List<TeeTime>> GolfRevTeeTimesWithPlaywright(GolfCourse course)
  {
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
      Headless = true,
      Args = new[] { "--no-sandbox" } // Required on Ubuntu EC2
    });

    var context = await browser.NewContextAsync(new BrowserNewContextOptions
    {
      UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36"
    });
    var page = await context.NewPageAsync();

    // Navigate to the booking page
    await page.GotoAsync(course.BookingUrl);

    // Wait for the tee time cards to be loaded on the page
    await page.WaitForSelectorAsync("div.card-body");

    // Extract tee time data from the DOM
    var teeTimes = await page.EvaluateAsync<List<TeeTime>>(@"() => {
            return Array.from(document.querySelectorAll('div.card-body')).map(card => {
                const time = card.querySelector('h5.card-title')?.innerText.trim();
                const courseName = card.querySelector('p.card-text.text-secondary')?.innerText.trim();
                const playersText = Array.from(card.querySelectorAll('p')).find(p => p.innerText.includes('players'))?.innerText.trim();
                const priceText = card.querySelector('p.cust-card-trim')?.innerText.trim();

                if (!time || !courseName || !playersText || !priceText) return null;

                const spotsMatch = playersText.match(/(\d+)/);
                const feeMatch = priceText.match(/\$(\d+(\.\d{1,2})?)/);

                if (!spotsMatch || !feeMatch) return null;

                return {
                    Time: time,
                    CourseName: courseName,
                    AvailableSpots: parseInt(spotsMatch[1]),
                    GreenFee: parseFloat(feeMatch[1])
                };
            }).filter(t => t !== null);
        }");

    return teeTimes;
  }

  private async Task InitializeForeUpSessionAsync(GolfCourse course)
  {
    var bookingPageUrl = course.BookingUrl;
    var initialRequest = new HttpRequestMessage(HttpMethod.Get, bookingPageUrl);

    initialRequest.Headers.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    initialRequest.Headers.Add("accept-language", "en-US,en;q=0.9");
    initialRequest.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    initialRequest.Headers.Referrer = new Uri("https://foreupsoftware.com/");

    var response = await _httpClient.SendAsync(initialRequest);
    response.EnsureSuccessStatusCode();
  }

  private async Task InitializeGolfRevSessionAsync(string bookingUrl)
  {
    var initialRequest = new HttpRequestMessage(HttpMethod.Get, bookingUrl);
    initialRequest.Headers.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    initialRequest.Headers.Add("accept-language", "en-US,en;q=0.9");
    initialRequest.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    initialRequest.Headers.Referrer = new Uri("https://www.golfrev.com/");

    var response = await _httpClient.SendAsync(initialRequest);
    response.EnsureSuccessStatusCode();
  }
}
