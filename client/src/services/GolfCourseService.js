import { GolfCourse } from "@/models/GolfCourse.js"
import { api } from "./AxiosService.js"
import { AppState } from "@/AppState.js"

class GolfCourseService{
  async getCourses() {
    const response = await api.get('https://localhost:7045/api/golf-courses')
    const courses = response.data.map(coursePOJO => new GolfCourse(coursePOJO))
    AppState.golfCourses = courses
  }

}

export const golfCourseService = new GolfCourseService()