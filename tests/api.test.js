/**
 * API Integration Tests
 *
 * Comprehensive test suite for File Conversion API endpoints.
 * Tests health checks, format validation, file conversion, static file serving,
 * and error handling scenarios. Ensures API behaves correctly under various conditions.
 */

const request = require("supertest");
const fs = require("fs");
const path = require("path");
const app = require("../src/server");

describe("File Conversion API", () => {
  /**
   * Setup test environment before running tests
   * Ensures temporary directory exists for file operations
   */
  beforeAll(() => {
    const tempDir = path.join(__dirname, "..", "temp");
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }
  });

  /**
   * Clean up temporary files after all tests complete
   * Removes test artifacts to prevent disk space issues
   */
  afterAll(() => {
    const tempDir = "./temp";
    if (fs.existsSync(tempDir)) {
      fs.rmSync(tempDir, { recursive: true, force: true });
    }
    // Note: In production/serverless, cleanup is automatic for /tmp
  });

  describe("Health Check", () => {
    /**
     * Test health check endpoint returns correct status
     * Verifies server is operational and returns expected response format
     */
    test("GET /api/health should return health status", async () => {
      const response = await request(app).get("/api/health").expect(200);

      expect(response.body).toHaveProperty("status", "healthy");
      expect(response.body).toHaveProperty("timestamp");
      expect(response.body).toHaveProperty("uptime");
      expect(response.body).toHaveProperty("version");
    });
  });

  describe("Supported Formats", () => {
    /**
     * Test supported formats endpoint returns complete format information
     * Verifies all expected format categories and structure are present
     */
    test("GET /api/supported-formats should return format information", async () => {
      const response = await request(app)
        .get("/api/supported-formats")
        .expect(200);

      expect(response.body).toHaveProperty("documents");
      expect(response.body).toHaveProperty("images");
      expect(response.body).toHaveProperty("audio");
      expect(response.body).toHaveProperty("video");
      expect(response.body).toHaveProperty("archives");

      // Verify documents section has expected structure
      expect(response.body.documents).toHaveProperty("input");
      expect(response.body.documents).toHaveProperty("conversions");
      expect(Array.isArray(response.body.documents.input)).toBe(true);
    });
  });

  describe("File Conversion", () => {
    /**
     * Test conversion endpoint without file upload
     * Verifies proper error handling for missing file parameter
     */
    test("POST /api/convert without file should return 400", async () => {
      const response = await request(app)
        .post("/api/convert")
        .field("targetFormat", "pdf")
        .expect(400);

      expect(response.body).toHaveProperty("error", "No file uploaded");
    });

    /**
     * Test conversion endpoint without target format
     * Verifies proper error handling for missing target format parameter
     */
    test("POST /api/convert without target format should return 400", async () => {
      // Create test file for upload
      const testContent = "This is a test file for conversion.";
      const testFilePath = path.join(__dirname, "test.txt");
      fs.writeFileSync(testFilePath, testContent);

      const response = await request(app)
        .post("/api/convert")
        .attach("file", testFilePath)
        .expect(400);

      expect(response.body).toHaveProperty("error", "Missing target format");

      // Clean up test file
      if (fs.existsSync(testFilePath)) {
        fs.unlinkSync(testFilePath);
      }
    });

    /**
     * Test conversion with unsupported format
     * Verifies proper error handling for invalid conversion requests
     */
    test("POST /api/convert with unsupported format should return 500", async () => {
      // Create test file for upload
      const testContent = "This is a test file.";
      const testFilePath = path.join(__dirname, "test.txt");
      fs.writeFileSync(testFilePath, testContent);

      const response = await request(app)
        .post("/api/convert")
        .attach("file", testFilePath)
        .field("targetFormat", "unsupported")
        .expect(500);

      expect(response.body).toHaveProperty("error");
      expect(response.body.error).toMatch(/failed|error/i);

      // Clean up test file
      if (fs.existsSync(testFilePath)) {
        fs.unlinkSync(testFilePath);
      }
    });
  });

  describe("Static Files", () => {
    /**
     * Test main page serving
     * Verifies HTML interface is served correctly
     */
    test("GET / should serve the main page", async () => {
      const response = await request(app).get("/").expect(200);

      expect(response.headers["content-type"]).toContain("text/html");
    });

    /**
     * Test JavaScript file serving
     * Verifies frontend JavaScript is served correctly
     */
    test("GET /app.js should serve the JavaScript file", async () => {
      const response = await request(app).get("/app.js").expect(200);

      expect(response.headers["content-type"]).toContain(
        "application/javascript"
      );
    });
  });

  describe("Error Handling", () => {
    /**
     * Test 404 handling for non-existent routes
     * Verifies proper error response for invalid GET requests
     */
    test("GET /nonexistent should return 404", async () => {
      const response = await request(app).get("/nonexistent").expect(404);

      expect(response.body).toHaveProperty("error", "Not found");
    });

    /**
     * Test 404 handling for non-existent API routes
     * Verifies proper error response for invalid POST requests
     */
    test("POST /api/nonexistent should return 404", async () => {
      const response = await request(app).post("/api/nonexistent").expect(404);

      expect(response.body).toHaveProperty("error", "Not found");
    });
  });
});
