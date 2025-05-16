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



  [HttpGet]
  public ActionResult<List<GolfCourse>> GetGolfCourses()
  {
    try
    {
      List<GolfCourse> golfCourses = _golfCourseService.GetGolfCourses();
      return Ok(golfCourses);
    }
    catch (Exception exception)
    {
      return BadRequest(exception.Message);

    }
  }
}


