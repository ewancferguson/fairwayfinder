
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
}