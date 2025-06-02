using Microsoft.AspNetCore.Mvc;

namespace fairwayfinder.Controllers;

[ApiController]
[Route("api/golf-courses")]
public class GolfCourseController : ControllerBase
{
  private readonly GolfCourseService _golfCourseService;

  public GolfCourseController(GolfCourseService golfCourseService)
  {
    _golfCourseService = golfCourseService;
  }

  [HttpGet]
  public ActionResult<List<GolfCourse>> GetGolfCourses()
  {
    try
    {
      var golfCourses = _golfCourseService.GetGolfCourses();
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
      var golfCourse = _golfCourseService.GetGolfCourseById(golfCourseId);
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
      var teeTimes = await _golfCourseService.GetTeeTimesAsync(courseId);
      return Ok(teeTimes);
    }
    catch (Exception ex)
    {
      return StatusCode(500, ex.Message);
    }
  }

  // --- New endpoint: Start GolfRev scrape job, returns a jobId immediately ---
  [HttpPost("{courseId}/golfrev/scrape")]
  public ActionResult<string> StartGolfRevScrapeJob(int courseId)
  {
    try
    {
      var jobId = _golfCourseService.StartGolfRevScrapeJob(courseId);
      return Ok(new { jobId });
    }
    catch (Exception ex)
    {
      return StatusCode(500, ex.Message);
    }
  }

  // --- New endpoint: Check GolfRev scrape job status by jobId ---
  [HttpGet("golfrev/scrape/{jobId}")]
  public ActionResult<object> GetGolfRevScrapeJobStatus(string jobId)
  {
    try
    {
      var (found, isComplete, results) = _golfCourseService.GetScrapeJobStatus(jobId);

      if (!found)
        return NotFound(new { message = "Job ID not found" });

      if (!isComplete)
        return Ok(new { status = "pending" });

      return Ok(new { status = "complete", teeTimes = results });
    }
    catch (Exception ex)
    {
      return StatusCode(500, ex.Message);
    }
  }
}
