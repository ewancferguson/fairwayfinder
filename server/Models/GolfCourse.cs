using Microsoft.OpenApi.Models;
using Microsoft.Playwright;

namespace fairwayfinder.Models

{
  public class GolfCourse
  {
    public int Id { get; set; }
    public string Name { get; set; }
    public string Img { get; set; }
    public string Location { get; set; }
    public string BookingSoftware { get; set; }
    public string FetchUrl { get; set; }
  }
}
