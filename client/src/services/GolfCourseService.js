import { GolfCourse } from "@/models/GolfCourse.js"
import { api } from "./AxiosService.js"
import { AppState } from "@/AppState.js"
import { TeeTime } from "@/models/TeeTime.js"
import { logger } from "@/utils/Logger.js"

class GolfCourseService {
  async getTeeTimes(courseId) {
    AppState.teeTimes = []
    const response = await api.get(`api/golf-courses/${courseId}/tee-times`)
    const teeTimes = response.data.map(teeTimePOJO => new TeeTime(teeTimePOJO))
    AppState.teeTimes = teeTimes
    logger.log('teeTimes', AppState.teeTimes)
  }
  async getCourseById(id) {
    AppState.activeGolfCourse = null
    const response = await api.get(`api/golf-courses/${id}`)
    const course = new GolfCourse(response.data)
    AppState.activeGolfCourse = course
  }
  async getCourses() {
    const response = await api.get('api/golf-courses')
    const courses = response.data.map(coursePOJO => new GolfCourse(coursePOJO))
    AppState.golfCourses = courses
  }

}

export const golfCourseService = new GolfCourseService()