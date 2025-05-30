using Microsoft.AspNetCore.Mvc.Infrastructure;

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

  [HttpGet("{golfCourseId}")]
  public ActionResult<GolfCourse> GetGolfCourseById(int golfCourseId)
  {
    try
    {
      GolfCourse golfCourse = _golfCourseService.GetGolfCourseById(golfCourseId);
      return Ok(golfCourse);
    }
    catch (Exception exception)
    {
      return BadRequest(exception.Message);

    }
  }

  [HttpGet("{courseId}/tee-times")]
  public async Task<ActionResult<List<TeeTime>>> GetTeeTimes(int courseId)
  {
    try
    {
      List<TeeTime> teeTimes = await _golfCourseService.GetTeeTimesAsync(courseId);
      return Ok(teeTimes);
    }
    catch (Exception ex)
    {
      return StatusCode(500, ex.Message);
    }
  }

}


