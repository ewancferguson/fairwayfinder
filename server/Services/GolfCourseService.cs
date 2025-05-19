using System.Globalization;
using System.Net.Http;
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

    if (course.BookingSoftware == "golfrev")
    {
      return await GolfRevTeeTimes(course);
    }

    throw new Exception("Unsupported booking software");
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
}
