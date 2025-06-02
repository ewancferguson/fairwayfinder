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

    private static readonly string[] UserAgents = new[]
    {
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36",
      "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.1 Safari/605.1.15",
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:108.0) Gecko/20100101 Firefox/108.0"
    };

    private static readonly ConcurrentDictionary<string, List<TeeTime>?> ScrapeJobStore = new();

    public GolfCourseService(GolfCourseRepository repository, IHttpClientFactory clientFactory)
    {
      _repository = repository;
      _httpClient = clientFactory.CreateClient();
    }

    public List<GolfCourse> GetGolfCourses() => _repository.GetGolfCourses();

    public GolfCourse GetGolfCourseById(int golfCourseId)
    {
      var course = _repository.GetGolfCourseById(golfCourseId);
      return course ?? throw new Exception("Golf Course does not exist");
    }

    public async Task<List<TeeTime>> GetTeeTimesAsync(int courseId)
    {
      var course = GetGolfCourseById(courseId);
      return course.BookingSoftware switch
      {
        "golfrev" => await GetGolfRevTeeTimesAsync(course),
        "foreup" => await GetForeupTeeTimesAsync(course),
        _ => throw new Exception("Unsupported booking software")
      };
    }

    public string StartScrapeJob(int courseId)
    {
      var jobId = Guid.NewGuid().ToString();
      ScrapeJobStore[jobId] = null;

      _ = Task.Run(async () =>
      {
        try
        {
          var course = GetGolfCourseById(courseId);
          var teeTimes = await GetTeeTimesAsync(courseId);
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

      return (true, teeTimes != null, teeTimes);
    }

    private HttpRequestMessage CreateRequest(string url, string referer)
    {
      var request = new HttpRequestMessage(HttpMethod.Get, url);
      request.Headers.Add("User-Agent", UserAgents[_rand.Next(UserAgents.Length)]);
      request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
      request.Headers.Add("Cache-Control", "no-cache");
      request.Headers.Add("Pragma", "no-cache");
      request.Headers.Referrer = new Uri(referer);
      return request;
    }

    private async Task<List<TeeTime>> GetGolfRevTeeTimesAsync(GolfCourse course)
    {
      await Task.Delay(_rand.Next(200, 500));
      var date = Uri.EscapeDataString(DateTime.Now.ToString("M/d/yyyy", CultureInfo.InvariantCulture));
      var url = course.FetchUrl.Replace("{DATE}", date);

      var response = await _httpClient.SendAsync(CreateRequest(url, "https://www.golfrev.com/go/tee_times"));
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

    private async Task<List<TeeTime>> GetForeupTeeTimesAsync(GolfCourse course)
    {
      await Task.Delay(_rand.Next(200, 500));
      var date = DateTime.Now.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
      var url = course.FetchUrl.Replace("{DATE}", date);

      var request = CreateRequest(url, course.BookingUrl);
      request.Headers.Add("accept", "application/json, text/javascript, */*; q=0.01");
      request.Headers.Add("x-fu-golfer-location", "foreup");
      request.Headers.Add("x-requested-with", "XMLHttpRequest");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();

      var json = await response.Content.ReadAsStringAsync();
      var raw = JsonSerializer.Deserialize<List<JsonElement>>(json);

      return raw?.Select(t =>
      {
        try
        {
          return new TeeTime
          {
            Time = DateTime.ParseExact(t.GetProperty("time").GetString()!, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).ToString("h:mm tt"),
            CourseName = t.GetProperty("course_name").GetString(),
            AvailableSpots = t.GetProperty("available_spots").GetInt32(),
            GreenFee = t.GetProperty("green_fee").GetDecimal()
          };
        }
        catch
        {
          return null;
        }
      }).Where(x => x != null).ToList() ?? new List<TeeTime>();
    }
  }
}
