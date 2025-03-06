
<template>
  <div class="container mx-auto p-6 max-w-2xl flex flex-col space-y-6">
    <!-- Text Prompt Section -->
    <div class="bg-white shadow-lg rounded-lg p-6">
      <h2 class="text-2xl font-semibold mb-4">Text Prompt</h2>
      <textarea v-model="textPrompt" class="w-full p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
        rows="4" placeholder="Enter your prompt here..."></textarea>
      <button @click="submitTextPrompt" class="btn-primary mt-4 w-full" :disabled="!textPrompt">
        Submit Prompt
      </button>
    </div>

    <!-- File Upload Section -->
    <div class="bg-white shadow-lg rounded-lg p-6">
      <h2 class="text-2xl font-semibold mb-4">File Upload</h2>
      <input type="file" @change="handleFileSelect" class="w-full border border-gray-300 p-2 rounded-lg">
      <button @click="uploadFile" class="btn-primary mt-4 w-full" :disabled="!selectedFile">
        Upload File
      </button>
    </div>

    <!-- Response Section -->
    <div v-if="response" class="response-box">
      <h3 class="text-xl font-bold mb-2">Response:</h3>
      <pre class="bg-gray-100 p-4 rounded-lg overflow-auto w-96">{{ response }}</pre>
    </div>

    <!-- Error Display -->
    <div v-if="error" class="error-box">
      {{ error }}
    </div>
  </div>
</template>


<script>
import { ref } from 'vue'
import axios from 'axios'


export default {
  name: 'App',
  methods: {

    async fetchStatus() {
      if (!this.processingId) return;
      try {
        var response = axios.get(`https://api.example.com/process/${this.processingId}/status`);
        this.processingStatus = response.data.status;

        if (response.data.completed == true) {
          if (this.interval) {
            clearInterval(this.interval);
          }
        }
      } catch (error) {
        console.error('Error fetching status:', error);
        if (this.interval) {
          clearInterval(this.interval);
        }
      }
    },
    pollStatus() {
      this.interval = setInterval(this.fetchStatus, 3000);
    }
  },
  beforeUnmount() {
    if (this.interval) {
      clearInterval(this.interval);
    }
  },
  setup() {
    var textPrompt = ref('')
    var selectedFile = ref(null)
    var response = ref(null)
    var error = ref(null)
    var statusId = ref(null)
    var pollfn = ref(null)
    var count=0;

    const submitTextPrompt = async () => {
      try {
        error.value = null
        var data = { "prompt": textPrompt.value }
        var res = await axios.post('/api/TextPromptFunction', JSON.stringify(data))
        response.value = res.data
        textPrompt.value = '' // Clear the input after successful submission
        statusId = res
      } catch (err) {
        error.value = err.response?.data?.message || 'An error occurred'
      }
      pollStatus();
    }

    const fetchStatus = async () => {
      if (!statusId) return;
      try {
        var r = await axios.get(`/api/status/` + statusId.data);
        if (r.data.response) {
          var s = r.data.response.join(' ')
          s = s.replace("[", "").replace("]", "").replace(",", "").replace("\n", "<br>")
          response.value = s
          if (r.data.completed == true)
            clearInterval(pollfn);
        }
        else 
        {
          count++;
        }
        if(count>5)
        {
          clearInterval(pollfn);
        }

      } catch (error) {
        console.error('Error fetching status:', error);
        if (pollfn) {
          clearInterval(pollfn);
        }
      }
    }



    const handleFileSelect = (event) => {
      selectedFile.value = event.target.files[0]
    }

    const pollStatus = async () => {
      pollfn = setInterval(fetchStatus, 1000);
    }

    const uploadFile = async () => {
      if (!selectedFile.value) return

      const formData = new FormData()
      formData.append('file', selectedFile.value)

      try {
        error.value = null
        const res = await axios.post('/api/FileUploadFunction', formData, {
          headers: {
            'Content-Type': 'multipart/form-data'
          }
        })
        response.value = res.data
        selectedFile.value = null // Clear the file input after successful upload
      } catch (err) {
        error.value = err.response?.data?.message || 'An error occurred'
      }
    }



    return {
      textPrompt,
      selectedFile,
      response,
      error,
      submitTextPrompt,
      handleFileSelect,
      uploadFile
    }
  },

}
</script>
<style scoped>
.btn-primary {
  background-color: #3b82f6;
  color: white;
  padding: 0.75rem;
  border-radius: 0.5rem;
  cursor: pointer;
  transition: background-color 0.2s;
  text-align: center;
  font-weight: bold;
}

.btn-primary:disabled {
  background-color: #93c5fd;
  cursor: not-allowed;
}

.response-box {
  margin-top: 1rem;
  padding: 1rem;
  background-color: #f3f4f6;
  border-radius: 0.5rem;
  width: 100%;
}

.error-box {
  margin-top: 1rem;
  padding: 1rem;
  background-color: #fee2e2;
  color: #b91c1c;
  border-radius: 0.5rem;
  text-align: center;
}
</style>