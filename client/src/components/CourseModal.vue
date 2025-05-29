<script setup>
import { AppState } from '@/AppState';
import { golfCourseService } from '@/services/GolfCourseService';
import { Pop } from '@/utils/Pop';
import { defineProps, defineEmits, computed, onMounted } from 'vue'

const course = computed(() => AppState.activeGolfCourse)
const teetimes = computed(() => AppState.teeTimes)

const emit = defineEmits(['close', 'bookTime'])





</script>

<template>
  <div v-if="course && teetimes" id="courseModal" class="modal fade" tabindex="-1"
    style="background-color: rgba(0, 0, 0, 0.5);" @click.self="emit('close')">
    <div class="modal-dialog modal-lg modal-dialog-centered">
      <div class="modal-content shadow-lg rounded-4">
        <div class="modal-header border-0">
          <h5 class="modal-title">{{ course.name }}</h5>
        </div>
        <div class="modal-body">
          <img :src="course.img || 'https://via.placeholder.com/800x400?text=No+Image'" alt="Course Image"
            class="img-fluid rounded mb-3" style="height: 250px; width: 100%; object-fit: cover;" />
          <p class="mb-1"><strong>Location:</strong> {{ course.location || 'Unknown location' }}</p>
          <a :href="course.bookingUrl">
            <button type="button" class="btn btn-success w-100 my-3">
              Book a Tee Time
            </button>
          </a>

          <section>
            <h6 class="mb-3">Available Tee Times Today</h6>
            <div class="teetimes-list d-flex flex-wrap gap-3">
              <!-- Placeholder tee time cards -->
              <div v-for="teetime in teetimes" :key="teetime.time" class="card p-2 flex-grow-1"
                style="min-width: 120px; max-width: 150px;">
                <div class="text-center">
                  <p class="mb-0 fw-semibold">{{ teetime.time }}</p>
                  <small class="text-muted">{{ teetime.availableSpots }}<span class="mdi mdi-account"></span></small>
                  <small class="text-muted"> ${{ teetime.greenFee }}</small>
                </div>
              </div>
              <div v-if="teetimes.length === 0" class="card p-2 flex-grow-1 text-center"
                style="min-width: 120px; max-width: 150px;">
                <p class="text-muted">No tee times available today</p>
              </div>
            </div>
          </section>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.modal-content {
  border-radius: 1rem;
}

.teetimes-list .card {
  background: #f8f9fa;
  border-radius: 0.5rem;
  box-shadow: 0 1px 5px rgba(0, 0, 0, 0.1);
  cursor: pointer;
  transition: transform 0.2s ease;
}

.teetimes-list .card:hover {
  transform: translateY(-5px);
  box-shadow: 0 8px 15px rgba(0, 0, 0, 0.15);
}
</style>
