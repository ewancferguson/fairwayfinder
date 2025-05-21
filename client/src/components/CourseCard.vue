<script setup>
import { GolfCourse } from '@/models/GolfCourse';



defineProps({
  course: {type: GolfCourse, required: true}
})





</script>


<template>
  <div class="card shadow-sm rounded-4 overflow-hidden h-100">
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
</template>


<style lang="scss" scoped>

  .card-img-top {
  height: 200px; // or any fixed height you want (e.g., 180px, 250px)
  object-fit: cover;
  object-position: center;
}


</style>