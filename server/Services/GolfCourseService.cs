using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using fairwayfinder.Models;
using fairwayfinder.Repositories;

namespace fairwayfinder.Services;

public class GolfCourseService
{
  private readonly GolfCourseRepository _repository;
  private readonly HttpClient _httpClient;
  private readonly CookieContainer _cookieContainer;
  private readonly Random _random = new();

  public GolfCourseService(GolfCourseRepository repository)
  {
    _repository = repository;
    _cookieContainer = new CookieContainer();

    var handler = new HttpClientHandler
    {
      CookieContainer = _cookieContainer,
      AllowAutoRedirect = true,
      AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };

    _httpClient = new HttpClient(handler);
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
    return course.BookingSoftware switch
    {
      "golfrev" => await GolfRevTeeTimes(course),
      "foreup" => await ForeupTeeTimes(course),
      _ => throw new Exception("Unsupported booking software")
    };
  }

  private void SetCommonHeaders(HttpRequestMessage request, string referrer)
  {
    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
    request.Headers.Referrer = new Uri(referrer);
  }

  private async Task<List<TeeTime>> GolfRevTeeTimes(GolfCourse course)
  {
    await InitializeGolfRevSessionAsync(course.BookingUrl);

    string today = DateTime.Now.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
    string encodedDate = Uri.EscapeDataString(today);
    string url = course.FetchUrl.Replace("{DATE}", encodedDate);

    var teeTimes = new List<TeeTime>();
    int attempts = 0;
    const int maxAttempts = 3;

    while (attempts < maxAttempts)
    {
      try
      {
        await Task.Delay(_random.Next(500, 1500)); // mimic human timing

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        SetCommonHeaders(request, "https://www.golfrev.com/go/tee_times/?r=1");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
          attempts++;
          await Task.Delay(1000 * attempts); // backoff
          continue;
        }

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card-body')]");
        if (cards == null) return teeTimes;

        foreach (var card in cards)
        {
          var time = card.SelectSingleNode(".//h5[contains(@class, 'card-title')]")?.InnerText.Trim();
          var courseName = card.SelectSingleNode(".//p[contains(@class, 'card-text text-secondary')]")?.InnerText.Trim();
          var playersText = card.SelectSingleNode(".//p[contains(text(), 'players')]")?.InnerText.Trim();
          var priceText = card.SelectSingleNode(".//p[contains(@class, 'cust-card-trim')]")?.InnerText.Trim();

          if (string.IsNullOrEmpty(time) || string.IsNullOrEmpty(courseName) ||
              string.IsNullOrEmpty(playersText) || string.IsNullOrEmpty(priceText)) continue;

          if (!int.TryParse(Regex.Match(playersText, @"(\d+)").Groups[1].Value, out var spots)) continue;
          if (!decimal.TryParse(Regex.Match(priceText, @"\$(\d+(\.\d{1,2})?)").Groups[1].Value, out var fee)) continue;

          teeTimes.Add(new TeeTime
          {
            Time = time,
            CourseName = courseName,
            AvailableSpots = spots,
            GreenFee = fee
          });
        }

        return teeTimes;
      }
      catch (Exception ex)
      {
        if (++attempts >= maxAttempts)
          throw new Exception($"GolfRev fetch failed after {maxAttempts} attempts: {ex.Message}");
        await Task.Delay(1000 * attempts);
      }
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
                CultureInfo.InvariantCulture).ToString("h:mm tt", CultureInfo.InvariantCulture),
          CourseName = t.GetProperty("course_name").GetString(),
          AvailableSpots = t.GetProperty("available_spots").GetInt32(),
          GreenFee = t.GetProperty("green_fee").GetDecimal()
        })
        .ToList();

    return teeTimes;
  }

  private async Task InitializeForeUpSessionAsync(GolfCourse course)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, course.BookingUrl);
    SetCommonHeaders(request, "https://foreupsoftware.com/");
    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
  }

  private async Task InitializeGolfRevSessionAsync(string bookingUrl)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, bookingUrl);
    SetCommonHeaders(request, "https://www.golfrev.com/");
    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
  }
}
