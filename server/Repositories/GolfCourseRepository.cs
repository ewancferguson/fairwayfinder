
using System.Net;

namespace fairwayfinder.Repositories;

public class GolfCourseRepository
{
  public GolfCourseRepository(IDbConnection db)
  {
    _db = db;
  }
  private readonly IDbConnection _db;

  public List<GolfCourse> GetGolfCourses()
  {
    string sql = "SELECT * FROM golfCourses";
    List<GolfCourse> golfCourses = _db.Query<GolfCourse>(sql).ToList();
    return golfCourses;
  }
}