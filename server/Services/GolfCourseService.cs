using System.Globalization;
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
      "golfrev" => await GolfRevTeeTimes(course),
      "foreup" => await ForeupTeeTimes(course),
      _ => throw new Exception("Unsupported booking software")
    };
  }

  private async Task<List<TeeTime>> ForeupTeeTimes(GolfCourse course)
  {
    await InitializeForeUpSessionAsync(course); // Make sure session/cookies are initialized

    string today = DateTime.Now.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
    string url = course.FetchUrl.Replace("{DATE}", today);

    var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("accept", "application/json, text/javascript, */*; q=0.01");
    request.Headers.Add("accept-language", "en-US,en;q=0.9");
    // request.Headers.Add("api-key", "no_limits"); // Uncomment if you know the API key
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




  private async Task<List<TeeTime>> GolfRevTeeTimes(GolfCourse course)
  {
    string today = DateTime.Now.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
    string encodedDate = Uri.EscapeDataString(today);

    string url = course.FetchUrl.Replace("{DATE}", encodedDate);

    try
    {
      var request = new HttpRequestMessage(HttpMethod.Get, url);
      request.Headers.Add("Accept", "text/html, */*; q=0.01");
      request.Headers.Add("X-Requested-With", "XMLHttpRequest");
      request.Headers.Add("User-Agent", "Mozilla/5.0");
      request.Headers.Referrer = new Uri("https://www.golfrev.com/go/tee_times/?r=1");

      var response = await _httpClient.SendAsync(request);
      response.EnsureSuccessStatusCode();

      var html = await response.Content.ReadAsStringAsync();
      var doc = new HtmlDocument();
      doc.LoadHtml(html);

      var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'card-body')]");
      if (cards == null) return new List<TeeTime>();

      var teeTimes = new List<TeeTime>();

      foreach (var card in cards)
      {
        var time = card.SelectSingleNode(".//h5[contains(@class, 'card-title')]")?.InnerText.Trim();
        var courseName = card.SelectSingleNode(".//p[contains(@class, 'card-text text-secondary')]")?.InnerText.Trim();
        var playersText = card.SelectSingleNode(".//p[contains(text(), 'players')]")?.InnerText.Trim();
        var priceText = card.SelectSingleNode(".//p[contains(@class, 'cust-card-trim')]")?.InnerText.Trim();

        if (string.IsNullOrEmpty(time) || string.IsNullOrEmpty(courseName) || string.IsNullOrEmpty(playersText) || string.IsNullOrEmpty(priceText))
          continue;

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
      throw new Exception($"GolfRev fetch failed: {ex.Message}");
    }
  }

  private async Task InitializeForeUpSessionAsync(GolfCourse course)
  {
    var bookingPageUrl = course.BookingUrl;
    var initialRequest = new HttpRequestMessage(HttpMethod.Get, bookingPageUrl);

    // Add typical headers...
    initialRequest.Headers.Add("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    initialRequest.Headers.Add("accept-language", "en-US,en;q=0.9");
    initialRequest.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    initialRequest.Headers.Referrer = new Uri("https://foreupsoftware.com/");

    var response = await _httpClient.SendAsync(initialRequest);
    response.EnsureSuccessStatusCode();
  }



}
