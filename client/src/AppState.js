import { reactive } from 'vue'

// NOTE AppState is a reactive object to contain app level data
export const AppState = reactive({
  /**@type {import('@bcwdev/auth0provider-client').Identity} */
  identity: null,
  /** @type {import('./models/Account.js').Account} user info from the database*/
  account: null,
  /** @type {import('./models/GolfCourse.js').GolfCourse[]} user info from the database*/
  golfCourses: [],
  /** @type {import('./models/GolfCourse.js').GolfCourse} user info from the database*/
  activeGolfCourse: null,
  /** @type {import('./models/TeeTime.js').TeeTime[]} user info from the database*/
  teeTimes : []
})

