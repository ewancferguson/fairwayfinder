using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using fairwayfinder.Models;
using fairwayfinder.Repositories;

namespace fairwayfinder.Services
{
  public class GolfCourseService
  {
    private readonly GolfCourseRepository _repository;
    private readonly HttpClient _httpClient;
    private static readonly Random _rand = new();

    private static readonly string[] UserAgents =
    {
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
      "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.1 Safari/605.1.15",
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0"
    };

    private static readonly ConcurrentDictionary<string, List<TeeTime>?> ScrapeJobs = new();

    public GolfCourseService(GolfCourseRepository repository, IHttpClientFactory httpClientFactory)
    {
      _repository = repository;
      _httpClient = httpClientFactory.CreateClient();
    }

    public List<GolfCourse> GetGolfCourses() => _repository.GetGolfCourses();

    public GolfCourse GetGolfCourseById(int id) =>
        _repository.GetGolfCourseById(id) ?? throw new Exception("Golf course not found.");

    public async Task<List<TeeTime>> GetTeeTimesAsync(int courseId)
    {
      var course = GetGolfCourseById(courseId);

      return course.BookingSoftware switch
      {
        "golfrev" => await GetGolfRevTeeTimesAsync(course),
        "foreup" => await ForeupTeeTimes(course),
        _ => throw new Exception("Unsupported booking software")
      };
    }

    public string StartScrapeJob(int courseId)
    {
      var jobId = Guid.NewGuid().ToString();
      ScrapeJobs[jobId] = null;

      _ = Task.Run(async () =>
      {
        try
        {
          var results = await GetTeeTimesAsync(courseId);
          ScrapeJobs[jobId] = results;
        }
        catch
        {
          ScrapeJobs.TryRemove(jobId, out _);
        }
      });

      return jobId;
    }

    public (bool Found, bool IsComplete, List<TeeTime>? Results) GetScrapeJobStatus(string jobId)
    {
      if (!ScrapeJobs.TryGetValue(jobId, out var results))
        return (false, false, null);

      return (true, results != null, results);
    }

    private HttpRequestMessage CreateRequest(string url, string referer)
    {
      var request = new HttpRequestMessage(HttpMethod.Get, url);
      var userAgent = UserAgents[_rand.Next(UserAgents.Length)];

      request.Headers.Add("User-Agent", userAgent);
      request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
      request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
      request.Headers.Add("Cache-Control", "no-cache");
      request.Headers.Add("Pragma", "no-cache");
      request.Headers.Add("Upgrade-Insecure-Requests", "1");
      request.Headers.Add("Sec-Fetch-Dest", "document");
      request.Headers.Add("Sec-Fetch-Mode", "navigate");
      request.Headers.Add("Sec-Fetch-Site", "same-origin");
      request.Headers.Add("Sec-Fetch-User", "?1");
      request.Headers.Referrer = new Uri(referer);

      return request;
    }

    private async Task<List<TeeTime>> GetGolfRevTeeTimesAsync(GolfCourse course)
    {
      await Task.Delay(_rand.Next(200, 500));
      var date = Uri.EscapeDataString(DateTime.Now.ToString("M/d/yyyy", CultureInfo.InvariantCulture));
      var url = course.FetchUrl.Replace("{DATE}", date);

      var request = CreateRequest(url, "https://www.golfrev.com/go/tee_times");
      var response = await _httpClient.SendAsync(request);

      Console.WriteLine($"GolfRev Request URL: {url}");
      Console.WriteLine($"Status Code: {response.StatusCode}");

      response.EnsureSuccessStatusCode();

      var html = await response.Content.ReadAsStringAsync();
      var doc = new HtmlDocument();
      doc.LoadHtml(html);

      var teeTimes = new List<TeeTime>();
      var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card-body')]");
      if (cards == null) return teeTimes;

      foreach (var card in cards)
      {
        try
        {
          var time = card.SelectSingleNode(".//h5[contains(@class, 'card-title')]")?.InnerText.Trim();
          var courseName = card.SelectSingleNode(".//p[contains(@class, 'text-secondary')]")?.InnerText.Trim();
          var players = Regex.Match(card.InnerText, @"(\d+) players").Groups[1].Value;
          var fee = Regex.Match(card.InnerText, @"\$(\d+(\.\d{1,2})?)").Groups[1].Value;

          if (int.TryParse(players, out var spots) && decimal.TryParse(fee, out var greenFee))
          {
            teeTimes.Add(new TeeTime
            {
              Time = time,
              CourseName = courseName,
              AvailableSpots = spots,
              GreenFee = greenFee
            });
          }
        }
        catch { continue; }
      }

      return teeTimes;
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
  }
}
