namespace fairwayfinder.Controllers;

[ApiController]
[Route("api/golf-courses")]

public class GolfCourseController : ControllerBase
{
  public GolfCourseController(GolfCourseService golfCourseService)
  {
    _golfCourseService = golfCourseService;
  }

  private readonly GolfCourseService _golfCourseService;
}


