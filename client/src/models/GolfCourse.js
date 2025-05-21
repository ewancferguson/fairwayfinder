export class GolfCourse {
  constructor(data) {
    this.id = data.id
    this.name = data.name
    this.img = data.img
    this.location = data.location
    this.bookingSoftware = data.bookingSoftware
    this.fetchUrl = data.fetchUrl
    this.bookingUrl = data.bookingUrl
  }
}