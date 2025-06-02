using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using HtmlAgilityPack;
using fairwayfinder.Models;
using fairwayfinder.Repositories;
using Microsoft.Playwright;

namespace fairwayfinder.Services;

public class GolfCourseService
{
  private readonly GolfCourseRepository _repository;
  private readonly HttpClient _httpClient;

  private static readonly ConcurrentDictionary<string, List<TeeTime>?> ScrapeJobStore = new();

  // Constructor: Accept HttpClient already configured with CookieContainer & decompression
  public GolfCourseService(GolfCourseRepository repository, HttpClient httpClient)
  {
    _repository = repository;
    _httpClient = httpClient;
  }

  public List<GolfCourse> GetGolfCourses() => _repository.GetGolfCourses();

  public GolfCourse GetGolfCourseById(int golfCourseId)
  {
    var golfCourse = _repository.GetGolfCourseById(golfCourseId);
    if (golfCourse == null) throw new Exception("Golf Course does not exist");
    return golfCourse;
  }

  public async Task<List<TeeTime>> GetTeeTimesAsync(int courseId)
  {
    var course = GetGolfCourseById(courseId);
    return course.BookingSoftware switch
    {
      "golfrev" => await GolfRevTeeTimes(course.Id),
      "foreup" => await ForeupTeeTimes(course),
      _ => throw new Exception("Unsupported booking software")
    };
  }

  public string StartGolfRevScrapeJob(int courseId)
  {
    var jobId = Guid.NewGuid().ToString();
    ScrapeJobStore[jobId] = null;

    _ = Task.Run(async () =>
    {
      try
      {
        var teeTimes = await GolfRevTeeTimes(courseId);
        ScrapeJobStore[jobId] = teeTimes;
      }
      catch
      {
        ScrapeJobStore.TryRemove(jobId, out _);
      }
    });

    return jobId;
  }

  public (bool Found, bool IsComplete, List<TeeTime>? Results) GetScrapeJobStatus(string jobId)
  {
    if (!ScrapeJobStore.TryGetValue(jobId, out var teeTimes))
      return (false, false, null);

    bool isComplete = teeTimes != null;
    return (true, isComplete, teeTimes);
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

    return rawTeeTimes
        .Where(t => t.TryGetProperty("time", out _) && t.TryGetProperty("course_name", out _) &&
                    t.TryGetProperty("available_spots", out _) && t.TryGetProperty("green_fee", out _))
        .Select(t => new TeeTime
        {
          Time = DateTime.ParseExact(
                t.GetProperty("time").GetString(),
                "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture).ToString("h:mm tt", CultureInfo.InvariantCulture),
          CourseName = t.GetProperty("course_name").GetString(),
          AvailableSpots = t.GetProperty("available_spots").GetInt32(),
          GreenFee = t.GetProperty("green_fee").GetDecimal()
        })
        .ToList();
  }

  public async Task<List<TeeTime>> GolfRevTeeTimes(int courseId)
  {
    var course = GetGolfCourseById(courseId);
    if (course == null) throw new Exception("Golf Course does not exist");

    // Initialize session and cookies first
    await InitializeGolfRevSessionAsync(course.BookingUrl);

    string today = DateTime.Now.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
    string encodedDate = Uri.EscapeDataString(today);
    string fetchUrl = course.FetchUrl.Replace("{DATE}", encodedDate);

    var request = new HttpRequestMessage(HttpMethod.Get, fetchUrl);

    // Add headers to mimic browser fetch
    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    request.Headers.Add("Accept", "text/html, */*; q=0.01");
    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
    request.Headers.Add("Cache-Control", "no-cache");
    request.Headers.Add("Pragma", "no-cache");
    request.Headers.Add("X-Requested-With", "XMLHttpRequest");
    request.Headers.Referrer = new Uri("https://www.golfrev.com/go/tee_times");

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var html = await response.Content.ReadAsStringAsync();

    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    var teeTimes = new List<TeeTime>();

    var cardBodies = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card-body')]");
    if (cardBodies == null) return teeTimes;

    foreach (var card in cardBodies)
    {
      var timeNode = card.SelectSingleNode(".//h5[contains(@class, 'card-title')]");
      var courseNameNode = card.SelectSingleNode(".//p[contains(@class, 'text-secondary')]");
      var playersNode = card.SelectSingleNode(".//p[contains(text(), 'players')]");
      var priceNode = card.SelectSingleNode(".//p[contains(@class, 'cust-card-trim')]");

      if (timeNode == null || courseNameNode == null || playersNode == null || priceNode == null)
        continue;

      var timeText = timeNode.InnerText.Trim();
      var courseNameText = courseNameNode.InnerText.Trim();
      var playersText = playersNode.InnerText.Trim();
      var priceText = priceNode.InnerText.Trim();

      if (!int.TryParse(Regex.Match(playersText, @"(\d+)").Groups[1].Value, out var spots)) continue;
      if (!decimal.TryParse(Regex.Match(priceText, @"\$(\d+(\.\d{1,2})?)").Groups[1].Value, out var fee)) continue;

      teeTimes.Add(new TeeTime
      {
        Time = timeText,
        CourseName = courseNameText,
        AvailableSpots = spots,
        GreenFee = fee
      });
    }

    return teeTimes;
  }

  private async Task InitializeForeUpSessionAsync(GolfCourse course)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, course.BookingUrl);
    request.Headers.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    request.Headers.Add("accept-language", "en-US,en;q=0.9");
    request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    request.Headers.Referrer = new Uri("https://foreupsoftware.com/");

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
  }

  private async Task InitializeGolfRevSessionAsync(string bookingUrl)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, bookingUrl);
    request.Headers.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    request.Headers.Add("accept-language", "en-US,en;q=0.9");
    request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    request.Headers.Referrer = new Uri("https://www.golfrev.com/");

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
  }
}
