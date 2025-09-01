/**
 * File Conversion Utility - Frontend Application
 *
 * Simplified frontend for the file conversion API
 * Handles file upload, format selection, and conversion process
 */

class FileConverter {
  constructor() {
    // Initialize state variables
    this.selectedFile = null;
    this.selectedFormat = null;
    this.supportedFormats = {};

    // Get DOM elements
    this.initializeElements();

    // Set up event listeners
    this.setupEventListeners();

    // Load initial data
    this.loadSupportedFormats();
    this.loadAppVersion();
  }

  /**
   * Initialize DOM element references
   */
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
    this.appVersionElement = document.getElementById("appVersion");
  }

  /**
   * Set up all event listeners
   */
  setupEventListeners() {
    // File upload events
    this.dropZone.addEventListener("click", () => this.fileInput.click());
    this.fileInput.addEventListener("change", (e) => this.handleFileSelect(e));

    // Drag and drop events
    this.dropZone.addEventListener("dragover", (e) => this.handleDragOver(e));
    this.dropZone.addEventListener("drop", (e) => this.handleDrop(e));
    this.dropZone.addEventListener("dragleave", (e) => this.handleDragLeave(e));

    // File management events
    this.removeFileBtn.addEventListener("click", () => this.removeFile());
    this.formatOptions.addEventListener("change", (e) =>
      this.handleFormatChange(e)
    );
    this.convertBtn.addEventListener("click", () => this.convertFile());
  }

  /**
   * Load supported format mappings from API
   */
  async loadSupportedFormats() {
    try {
      console.log("Loading supported formats...");
      const response = await fetch("/api/supported-formats");
      this.supportedFormats = await response.json();
      console.log("Supported formats loaded:", this.supportedFormats);
    } catch (error) {
      console.error("Failed to load supported formats:", error);
    }
  }

  /**
   * Load application version from health endpoint
   */
  async loadAppVersion() {
    try {
      const response = await fetch("/api/health");
      const data = await response.json();
      if (data && data.version) {
        this.appVersionElement.textContent = `Version ${data.version}`;
      }
    } catch (error) {
      console.error("Failed to load app version:", error);
      this.appVersionElement.textContent = "Version N/A";
    }
  }

  /**
   * Handle file selection from file input
   */
  handleFileSelect(event) {
    const file = event.target.files[0];
    if (file) {
      this.selectFile(file);
    }
  }

  /**
   * Handle drag over event
   */
  handleDragOver(event) {
    event.preventDefault();
    this.dropZone.classList.add("dragover");
  }

  /**
   * Handle drag leave event
   */
  handleDragLeave(event) {
    event.preventDefault();
    this.dropZone.classList.remove("dragover");
  }

  /**
   * Handle file drop event
   */
  handleDrop(event) {
    event.preventDefault();
    this.dropZone.classList.remove("dragover");

    const files = event.dataTransfer.files;
    if (files.length > 0) {
      this.selectFile(files[0]);
    }
  }

  /**
   * Select a file and update UI
   */
  selectFile(file) {
    console.log("selectFile called with:", file);
    this.selectedFile = file;
    this.updateFileInfo();
    this.showFormatSelection();
    this.populateFormatOptions();
  }

  /**
   * Update file information display
   */
  updateFileInfo() {
    console.log("updateFileInfo called");
    this.fileName.textContent = this.selectedFile.name;
    this.fileDetails.textContent = this.formatFileSize(this.selectedFile.size);
    this.fileInfo.style.display = "block";
    console.log("File info updated, display set to block");
  }

  /**
   * Show format selection section
   */
  showFormatSelection() {
    console.log("showFormatSelection called");
    this.formatSelection.style.display = "block";
    this.convertBtn.style.display = "block";
    console.log("Format selection and convert button displayed");
  }

  /**
   * Populate format options based on selected file
   */
  populateFormatOptions() {
    console.log("populateFormatOptions called");
    const fileExt = this.getFileExtension(this.selectedFile.name);
    console.log("File extension:", fileExt);
    const availableFormats = this.getAvailableFormats(fileExt);
    console.log("Available formats:", availableFormats);

    this.formatOptions.innerHTML = '<option value="">Choose format...</option>';
    availableFormats.forEach((format) => {
      const option = document.createElement("option");
      option.value = format;
      option.textContent = format.toUpperCase();
      this.formatOptions.appendChild(option);
    });
    console.log("Format options populated");
  }

  /**
   * Get available conversion formats for a file type
   */
  getAvailableFormats(fileExt) {
    // Check all conversion mappings for the file extension
    for (const category in this.supportedFormats) {
      if (this.supportedFormats[category][fileExt]) {
        return this.supportedFormats[category][fileExt];
      }
    }
    return [];
  }

  /**
   * Handle format selection change
   */
  handleFormatChange(event) {
    this.selectedFormat = event.target.value;
    this.convertBtn.disabled = !this.selectedFormat;
  }

  /**
   * Remove selected file and reset UI
   */
  removeFile() {
    this.selectedFile = null;
    this.selectedFormat = null;
    this.fileInput.value = "";

    this.fileInfo.style.display = "none";
    this.formatSelection.style.display = "none";
    this.convertBtn.style.display = "none";
    this.progressContainer.style.display = "none";

    this.convertBtn.disabled = true;
  }

  /**
   * Convert the selected file
   */
  async convertFile() {
    if (!this.selectedFile || !this.selectedFormat) {
      return;
    }

    this.showProgress();
    this.updateProgress(10, "Preparing conversion...");

    try {
      const formData = new FormData();
      formData.append("file", this.selectedFile);
      formData.append("targetFormat", this.selectedFormat);

      this.updateProgress(30, "Uploading file...");

      const response = await fetch("/api/convert", {
        method: "POST",
        body: formData,
      });

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      this.updateProgress(70, "Processing conversion...");

      const result = await response.blob();

      this.updateProgress(100, "Conversion complete!");

      // Download the converted file
      setTimeout(() => {
        this.downloadFile(result, this.getOutputFilename());
        this.hideProgress();
      }, 1000);
    } catch (error) {
      console.error("Conversion error:", error);
      this.updateProgress(0, `Error: ${error.message}`);

      setTimeout(() => {
        this.hideProgress();
      }, 3000);
    }
  }

  /**
   * Show progress container
   */
  showProgress() {
    this.progressContainer.style.display = "block";
    this.convertBtn.disabled = true;
  }

  /**
   * Hide progress container
   */
  hideProgress() {
    this.progressContainer.style.display = "none";
    this.convertBtn.disabled = false;
  }

  /**
   * Update progress bar and text
   */
  updateProgress(percentage, text) {
    this.progressBar.style.width = `${percentage}%`;
    this.progressText.textContent = text;
  }

  /**
   * Download converted file
   */
  downloadFile(blob, filename) {
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
  }

  /**
   * Generate output filename
   */
  getOutputFilename() {
    const baseName = this.selectedFile.name.replace(/\.[^/.]+$/, "");
    return `${baseName}.${this.selectedFormat}`;
  }

  /**
   * Get file extension from filename
   */
  getFileExtension(filename) {
    return filename.split(".").pop().toLowerCase();
  }

  /**
   * Format file size in human readable format
   */
  formatFileSize(bytes) {
    if (bytes === 0) return "0 Bytes";

    const k = 1024;
    const sizes = ["Bytes", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i];
  }
}

// Initialize the application when DOM is loaded
document.addEventListener("DOMContentLoaded", () => {
  console.log("DOM loaded, initializing FileConverter...");
  new FileConverter();
  console.log("FileConverter initialized");
});
