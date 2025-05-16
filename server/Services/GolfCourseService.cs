namespace fairwayfinder.Services;


public class GolfCourseService
{
  public GolfCourseService(GolfCourseRepository repository)
  {
    _repository = repository;
  }

  private readonly GolfCourseRepository _repository;
}