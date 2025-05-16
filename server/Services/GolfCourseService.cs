


using System.Globalization;

namespace fairwayfinder.Services;


public class GolfCourseService
{
  public GolfCourseService(GolfCourseRepository repository)
  {
    _repository = repository;
  }

  private readonly GolfCourseRepository _repository;

  internal List<GolfCourse> GetGolfCourses()
  {
    List<GolfCourse> golfCourses = _repository.GetGolfCourses();

    return golfCourses;
  }

  internal GolfCourse GetGolfCourseById(int golfCourseId)
  {
    GolfCourse golfCourse = _repository.GetGolfCourseById(golfCourseId);
    if (golfCourse == null) throw new Exception("Golf Course does not exist");
    return golfCourse;
  }

  // internal async Task GetTeeTimesAsync(int courseId)
  // {
  //   GolfCourse course = GetGolfCourseById(courseId);

  //   if (course.BookingSoftware == "golfrev")
  //   {

  //   }
  // }
}