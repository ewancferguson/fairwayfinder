<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const slides = [
  {
    image: 'https://images.unsplash.com/photo-1535131749006-b7f58c99034b?fm=jpg&q=60&w=3000&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxzZWFyY2h8NHx8Z29sZnxlbnwwfHwwfHx8MA%3D%3D',
    heading: 'Welcome to Fairway Finder',
    text: 'Discover tee times effortlessly — all your favorite golf courses in one convenient place.'
  },
  {
    image: 'https://images.unsplash.com/photo-1500932334442-8761ee4810a7?q=80&w=2070&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D',
    heading: 'Find Tee Times Instantly',
    text: 'Search across hundreds of golf courses and compare available tee times in real-time to book your perfect round.'
  },
  {
    image: 'https://images.unsplash.com/photo-1632946269126-0f8edbe8b068?q=80&w=2031&auto=format&fit=crop&ixlib=rb-4.1.0&ixid=M3wxMjA3fDB8MHxwaG90by1wYWdlfHx8fGVufDB8fHx8fA%3D%3D',
    heading: 'Save Time, Play More',
    text: 'No more switching between multiple sites — manage your bookings and discover new courses all from one easy-to-use platform.'
  }
]

const currentIndex = ref(0)
let intervalId

onMounted(() => {
  intervalId = setInterval(() => {
    currentIndex.value = (currentIndex.value + 1) % slides.length
  }, 10000)
})

onUnmounted(() => {
  clearInterval(intervalId)
})
</script>

<template>
  <div class="container-fluid p-0">
    <div class="hero-section">
      <transition name="fade" mode="out-in">
        <div
          :key="currentIndex"
          class="hero-slide"
          :style="{ backgroundImage: `url('${slides[currentIndex].image}')` }"
        >
          <div class="hero-text">
            <h1>{{ slides[currentIndex].heading }}</h1>
            <p>{{ slides[currentIndex].text }}</p>
          </div>
        </div>
      </transition>
    </div>

    <section class="how-it-works text-center py-5">
      <h2 class="section-title">How It Works</h2>
      <div class="steps d-flex justify-content-center gap-4 flex-wrap mt-4">
        <div class="step px-3">
          <div class="icon">🔍</div>
          <h3>Search Courses</h3>
          <p>Enter your location or favorite course to find available tee times.</p>
        </div>
        <div class="step px-3">
          <div class="icon">📊</div>
          <h3>Compare Options</h3>
          <p>View real-time tee times across multiple courses side-by-side.</p>
        </div>
        <div class="step px-3">
          <div class="icon">⛳️</div>
          <h3>Plan Your Round</h3>
          <p>Choose your preferred time and get ready to hit the fairway. Booking coming soon!</p>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped lang="scss">
.container-fluid {
  padding-left: 0;
  padding-right: 0;
}

.hero-section {
  height: 60vh;
  position: relative;
  overflow: hidden;
}

.hero-slide {
  width: 100%;
  height: 100%;
  background-size: cover;
  background-position: center;
  background-repeat: no-repeat;
  position: relative;
}

.hero-text {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 2;
  color: white;
  text-shadow: 0 2px 8px rgba(0, 0, 0, 0.6);
  text-align: center;
  padding: 1rem;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 1s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* How It Works Section */
.how-it-works {
  background: #f9f9f9;
}

.section-title {
  font-weight: 700;
  font-size: 2.25rem;
  margin-bottom: 1.5rem;
  color: #333;
}

.steps {
  max-width: 900px;
  margin: 0 auto;
}

.step {
  max-width: 250px;
  background: white;
  border-radius: 8px;
  box-shadow: 0 3px 10px rgba(0,0,0,0.1);
  padding: 1.5rem 1rem;
}

.step .icon {
  font-size: 2.5rem;
  margin-bottom: 1rem;
}

.step h3 {
  font-weight: 600;
  margin-bottom: 0.75rem;
  color: #222;
}

.step p {
  color: #555;
  font-size: 0.95rem;
  line-height: 1.4;
}

/* Responsive */
@media (max-width: 768px) {
  .steps {
    flex-direction: column;
    gap: 1.5rem;
  }
}
</style>
