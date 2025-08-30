const request = require("supertest");
const fs = require("fs");
const path = require("path");
const app = require("../src/server");

describe("File Conversion API", () => {
  // Clean up temp files after tests
  afterAll(() => {
    const tempDir = "./temp";
    if (fs.existsSync(tempDir)) {
      fs.rmSync(tempDir, { recursive: true, force: true });
    }
    // Note: In production/serverless, cleanup is automatic for /tmp
  });

  describe("Health Check", () => {
    test("GET /api/health should return health status", async () => {
      const response = await request(app).get("/api/health").expect(200);

      expect(response.body).toHaveProperty("status", "healthy");
      expect(response.body).toHaveProperty("timestamp");
      expect(response.body).toHaveProperty("uptime");
      expect(response.body).toHaveProperty("version");
    });
  });

  describe("Supported Formats", () => {
    test("GET /api/supported-formats should return format information", async () => {
      const response = await request(app)
        .get("/api/supported-formats")
        .expect(200);

      expect(response.body).toHaveProperty("documents");
      expect(response.body).toHaveProperty("images");
      expect(response.body).toHaveProperty("audio");
      expect(response.body).toHaveProperty("video");
      expect(response.body).toHaveProperty("archives");

      // Check that documents has expected structure
      expect(response.body.documents).toHaveProperty("input");
      expect(response.body.documents).toHaveProperty("conversions");
      expect(Array.isArray(response.body.documents.input)).toBe(true);
    });
  });

  describe("File Conversion", () => {
    test("POST /api/convert without file should return 400", async () => {
      const response = await request(app)
        .post("/api/convert")
        .field("targetFormat", "pdf")
        .expect(400);

      expect(response.body).toHaveProperty("error", "No file uploaded");
    });

    test("POST /api/convert without target format should return 400", async () => {
      // Create a simple test file
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

    test("POST /api/convert with valid TXT to PDF should work", async () => {
      // Create a simple test file
      const testContent =
        "This is a test file for PDF conversion.\nIt has multiple lines.\nAnd should convert successfully.";
      const testFilePath = path.join(__dirname, "test.txt");
      fs.writeFileSync(testFilePath, testContent);

      const response = await request(app)
        .post("/api/convert")
        .attach("file", testFilePath)
        .field("targetFormat", "pdf")
        .expect(200);

      // Should return a PDF file
      expect(response.headers["content-type"]).toContain("application");
      expect(response.body.length).toBeGreaterThan(0);

      // Clean up test file
      if (fs.existsSync(testFilePath)) {
        fs.unlinkSync(testFilePath);
      }
    }, 10000); // Increase timeout for conversion

    test("POST /api/convert with unsupported format should return 500", async () => {
      // Create a simple test file
      const testContent = "This is a test file.";
      const testFilePath = path.join(__dirname, "test.txt");
      fs.writeFileSync(testFilePath, testContent);

      const response = await request(app)
        .post("/api/convert")
        .attach("file", testFilePath)
        .field("targetFormat", "unsupported")
        .expect(500);

      expect(response.body).toHaveProperty("error", "Conversion failed");

      // Clean up test file
      if (fs.existsSync(testFilePath)) {
        fs.unlinkSync(testFilePath);
      }
    });
  });

  describe("Static Files", () => {
    test("GET / should serve the main page", async () => {
      const response = await request(app).get("/").expect(200);

      expect(response.headers["content-type"]).toContain("text/html");
    });

    test("GET /app.js should serve the JavaScript file", async () => {
      const response = await request(app).get("/app.js").expect(200);

      expect(response.headers["content-type"]).toContain(
        "application/javascript"
      );
    });
  });

  describe("Error Handling", () => {
    test("GET /nonexistent should return 404", async () => {
      const response = await request(app).get("/nonexistent").expect(404);

      expect(response.body).toHaveProperty("error", "Not found");
    });

    test("POST /api/nonexistent should return 404", async () => {
      const response = await request(app).post("/api/nonexistent").expect(404);

      expect(response.body).toHaveProperty("error", "Not found");
    });
  });
});
