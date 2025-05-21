<script setup>
import { AppState } from '@/AppState';
import { GolfCourse } from '@/models/GolfCourse';
import { golfCourseService } from '@/services/GolfCourseService';
import { computed } from 'vue';
import CourseModal from './CourseModal.vue';
import { Modal } from 'bootstrap';
import { Pop } from '@/utils/Pop';



defineProps({
  course: {type: GolfCourse, required: true}
})

async function getCourseById(id) {
  try {
    await golfCourseService.getCourseById(id)
    getTeeTimes(id)
    Modal.getOrCreateInstance('#courseModal').show()
  } catch (error) {
    console.error('Error fetching course:', error)
  }
}


async function getTeeTimes(courseId) {
  try {
    await golfCourseService.getTeeTimes(courseId)
    
  } catch (error) {
    Pop.error(error);
  }
  
}




</script>


<template>
  <div v-if="course" @click="getCourseById(course.id)" class="card shadow-sm rounded-4 overflow-hidden h-100">
    <img
      :src="course.img"
      class="card-img-top"
      alt="Golf Course"
    />
    <div class="card-body text-center">
      <h5 class="card-title mb-1">{{ course.name }}</h5>
      <p class="card-text text-muted">{{ course.location }}</p>
    </div>
  </div>
  <CourseModal/>
</template>


<style lang="scss" scoped>

  .card-img-top {
  height: 200px; // or any fixed height you want (e.g., 180px, 250px)
  object-fit: cover;
  object-position: center;
}


</style>