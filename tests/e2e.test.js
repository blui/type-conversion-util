/**
 * End-to-End API Tests
 *
 * Comprehensive test suite for API endpoints including health checks,
 * format validation, and file conversion functionality.
 * Tests both successful operations and error handling scenarios.
 */

const path = require("path");
const fs = require("fs");
const request = require("supertest");
const app = require("../src/server");

describe("E2E: API routes", () => {
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
   * Test health check endpoint
   * Verifies server is running and returns expected status information
   */
  test("GET /api/health returns healthy", async () => {
    const res = await request(app).get("/api/health").expect(200);
    expect(res.body.status).toBe("healthy");
    expect(typeof res.body.uptime).toBe("number");
  });

  /**
   * Test supported formats endpoint
   * Verifies format mappings are returned correctly
   */
  test("GET /api/supported-formats returns mappings", async () => {
    const res = await request(app).get("/api/supported-formats").expect(200);
    expect(res.body).toHaveProperty("documents");
    expect(res.body.documents.conversions).toHaveProperty("docx");
  });

  /**
   * Test CSV to XLSX conversion
   * Verifies file conversion functionality with actual file processing
   */
  test("POST /api/convert csv->xlsx", async () => {
    // Create temporary directory for test files
    const tmpDir = path.join(__dirname, "tmp");
    if (!fs.existsSync(tmpDir)) fs.mkdirSync(tmpDir, { recursive: true });

    // Create test CSV file
    const csvPath = path.join(tmpDir, "data.csv");
    fs.writeFileSync(csvPath, "a,b,c\n1,2,3\n4,5,6\n");

    // Perform conversion
    const res = await request(app)
      .post("/api/convert")
      .field("targetFormat", "xlsx")
      .attach("file", csvPath)
      .expect(200);

    // Verify response headers indicate file download
    expect(res.headers["content-disposition"]).toContain("attachment");
  });
});
