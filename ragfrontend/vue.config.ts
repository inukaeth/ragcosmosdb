module.exports = {
    server: {
      proxy: {
        '^/api': {
          target: ' http://localhost:7020',
          changeOrigin: true,         
        }
      }
    }
  }