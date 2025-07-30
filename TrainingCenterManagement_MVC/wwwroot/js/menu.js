document.addEventListener("DOMContentLoaded", () => {
    // Category switching
    const categoryButtons = document.querySelectorAll(".category-btn")
    const menuSections = document.querySelectorAll(".menu-section")
  
    categoryButtons.forEach((button) => {
      button.addEventListener("click", function () {
        const category = this.dataset.category
  
        // Update active button
        categoryButtons.forEach((btn) => btn.classList.remove("active"))
        this.classList.add("active")
  
        // Show selected section
        menuSections.forEach((section) => {
          if (section.id === category) {
            section.classList.add("active")
          } else {
            section.classList.remove("active")
          }
        })
      })
    })
  
    // Share menu functionality
    document.getElementById("share-menu").addEventListener("click", () => {
      if (navigator.share) {
        navigator
          .share({
            title: "Café Delicious Menu",
            text: "Check out the menu at Café Delicious!",
            url: window.location.href,
          })
          .catch((error) => console.log("Error sharing:", error))
      } else {
        alert("Web Share API not supported in your browser. You can copy the URL to share.")
      }
    })
  
    // Feedback button
    document.getElementById("feedback-btn").addEventListener("click", () => {
      alert("Thank you for your interest! The feedback feature will be available soon.")
    })
  })
  