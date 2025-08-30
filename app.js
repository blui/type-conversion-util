class FileConverter {
  constructor() {
    this.selectedFile = null;
    this.selectedFormat = null;
    this.supportedFormats = {};

    this.initializeElements();
    this.setupEventListeners();
    this.loadSupportedFormats();
  }

  initializeElements() {
    this.dropZone = document.getElementById("dropZone");
    this.fileInput = document.getElementById("fileInput");
    this.fileInfo = document.getElementById("fileInfo");
    this.fileName = document.getElementById("fileName");
    this.fileDetails = document.getElementById("fileDetails");
    this.removeFileBtn = document.getElementById("removeFile");
    this.formatSelection = document.getElementById("formatSelection");
    this.formatOptions = document.getElementById("formatOptions");
    this.convertBtn = document.getElementById("convertBtn");
    this.progressContainer = document.getElementById("progressContainer");
    this.progressBar = document.getElementById("progressBar");
    this.progressText = document.getElementById("progressText");
  }

  setupEventListeners() {
    // Drop zone events
    this.dropZone.addEventListener("click", () => this.fileInput.click());
    this.dropZone.addEventListener("dragover", this.handleDragOver.bind(this));
    this.dropZone.addEventListener(
      "dragleave",
      this.handleDragLeave.bind(this)
    );
    this.dropZone.addEventListener("drop", this.handleDrop.bind(this));

    // File input change
    this.fileInput.addEventListener("change", this.handleFileSelect.bind(this));

    // Remove file button
    this.removeFileBtn.addEventListener("click", this.removeFile.bind(this));

    // Convert button
    this.convertBtn.addEventListener("click", this.convertFile.bind(this));
  }

  async loadSupportedFormats() {
    try {
      const response = await fetch("/api/supported-formats");
      this.supportedFormats = await response.json();
    } catch (error) {
      console.error("Failed to load supported formats:", error);
      this.showAlert("Failed to load supported formats", "danger");
    }
  }

  handleDragOver(e) {
    e.preventDefault();
    this.dropZone.classList.add("dragover");
  }

  handleDragLeave(e) {
    e.preventDefault();
    this.dropZone.classList.remove("dragover");
  }

  handleDrop(e) {
    e.preventDefault();
    this.dropZone.classList.remove("dragover");

    const files = e.dataTransfer.files;
    if (files.length > 0) {
      this.processFile(files[0]);
    }
  }

  handleFileSelect(e) {
    const files = e.target.files;
    if (files.length > 0) {
      this.processFile(files[0]);
    }
  }

  processFile(file) {
    this.selectedFile = file;

    // Show file information
    this.fileName.textContent = file.name;
    this.fileDetails.textContent = `${this.formatFileSize(file.size)} • ${
      file.type || "Unknown type"
    }`;

    this.fileInfo.classList.remove("d-none");
    this.formatSelection.classList.remove("d-none");

    // Generate format options
    this.generateFormatOptions(file);
  }

  generateFormatOptions(file) {
    const fileExtension = this.getFileExtension(file.name).toLowerCase();
    const availableFormats = this.getAvailableFormats(fileExtension);

    this.formatOptions.innerHTML = "";

    if (availableFormats.length === 0) {
      this.formatOptions.innerHTML =
        '<p class="text-muted">No conversions available for this file type.</p>';
      return;
    }

    availableFormats.forEach((format) => {
      const button = document.createElement("button");
      button.className = "btn btn-outline-primary me-2 mb-2";
      button.textContent = format.toUpperCase();
      button.addEventListener("click", () => this.selectFormat(format, button));
      this.formatOptions.appendChild(button);
    });
  }

  getAvailableFormats(fileExtension) {
    const formats = [];

    // Check each category for available conversions
    Object.values(this.supportedFormats).forEach((category) => {
      if (category.conversions && category.conversions[fileExtension]) {
        formats.push(...category.conversions[fileExtension]);
      }
    });

    return [...new Set(formats)]; // Remove duplicates
  }

  selectFormat(format, button) {
    // Remove active class from all buttons
    this.formatOptions.querySelectorAll(".btn").forEach((btn) => {
      btn.classList.remove("btn-primary");
      btn.classList.add("btn-outline-primary");
    });

    // Add active class to selected button
    button.classList.remove("btn-outline-primary");
    button.classList.add("btn-primary");

    this.selectedFormat = format;
    this.convertBtn.disabled = false;
  }

  async convertFile() {
    if (!this.selectedFile || !this.selectedFormat) {
      this.showAlert("Please select a file and target format", "warning");
      return;
    }

    const formData = new FormData();
    formData.append("file", this.selectedFile);
    formData.append("targetFormat", this.selectedFormat);

    try {
      this.showProgress();
      this.convertBtn.disabled = true;

      const response = await fetch("/api/convert", {
        method: "POST",
        body: formData,
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || "Conversion failed");
      }

      // Download the converted file
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${this.getFileNameWithoutExtension(
        this.selectedFile.name
      )}.${this.selectedFormat}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);

      this.showAlert("File converted successfully!", "success");
      this.hideProgress();
    } catch (error) {
      console.error("Conversion error:", error);
      this.showAlert(`Conversion failed: ${error.message}`, "danger");
      this.hideProgress();
    } finally {
      this.convertBtn.disabled = false;
    }
  }

  removeFile() {
    this.selectedFile = null;
    this.selectedFormat = null;
    this.fileInput.value = "";

    this.fileInfo.classList.add("d-none");
    this.formatSelection.classList.add("d-none");
    this.hideProgress();

    this.convertBtn.disabled = true;
  }

  showProgress() {
    this.progressContainer.style.display = "block";
    this.progressBar.style.width = "0%";
    this.progressText.textContent = "0%";

    // Simulate progress (since we don't have real progress from the server)
    let progress = 0;
    const interval = setInterval(() => {
      progress += Math.random() * 15;
      if (progress > 90) progress = 90;

      this.progressBar.style.width = `${progress}%`;
      this.progressText.textContent = `${Math.round(progress)}%`;
    }, 200);

    // Store interval ID to clear it later
    this.progressInterval = interval;
  }

  hideProgress() {
    if (this.progressInterval) {
      clearInterval(this.progressInterval);
    }

    this.progressBar.style.width = "100%";
    this.progressText.textContent = "100%";

    setTimeout(() => {
      this.progressContainer.style.display = "none";
    }, 1000);
  }

  showAlert(message, type = "info") {
    // Remove existing alerts
    const existingAlerts = document.querySelectorAll(".alert");
    existingAlerts.forEach((alert) => alert.remove());

    const alert = document.createElement("div");
    alert.className = `alert alert-${type} alert-dismissible fade show`;
    alert.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

    // Insert after the hero section
    const heroSection = document.querySelector(".hero-section");
    heroSection.insertAdjacentElement("afterend", alert);

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
      if (alert.parentNode) {
        alert.remove();
      }
    }, 5000);
  }

  getFileExtension(filename) {
    return filename.split(".").pop() || "";
  }

  getFileNameWithoutExtension(filename) {
    return filename.substring(0, filename.lastIndexOf(".")) || filename;
  }

  formatFileSize(bytes) {
    if (bytes === 0) return "0 Bytes";

    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  }
}

// Initialize the application when the DOM is loaded
document.addEventListener("DOMContentLoaded", () => {
  new FileConverter();
});
