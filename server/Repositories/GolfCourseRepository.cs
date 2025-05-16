namespace fairwayfinder.Repositories;

public class GolfCourseRepository
{
  public GolfCourseRepository(IDbConnection db)
  {
    _db = db;
  }
  private readonly IDbConnection _db;
}