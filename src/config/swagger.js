/**
 * Swagger/OpenAPI Documentation Configuration
 *
 * Configures and sets up interactive API documentation using Swagger UI.
 * Loads the OpenAPI specification from YAML file and provides endpoints
 * for accessing the documentation in multiple formats.
 */

const swaggerUi = require("swagger-ui-express");
const yaml = require("js-yaml");
const fs = require("fs");
const path = require("path");

/**
 * Load and parse OpenAPI specification
 * Attempts to load the specification from YAML file with fallback handling
 */
let swaggerDocument;
try {
  swaggerDocument = yaml.load(
    fs.readFileSync(path.join(__dirname, "../../docs/openapi.yaml"), "utf8")
  );

  /**
   * Update server URLs based on current environment
   * Dynamically sets the correct server URL for API testing
   */
  if (swaggerDocument.servers) {
    swaggerDocument.servers = [
      {
        url:
          process.env.NODE_ENV === "production"
            ? "https://api.example.com/api"
            : "http://localhost:3000/api",
        description:
          process.env.NODE_ENV === "production"
            ? "Production server"
            : "Development server",
      },
    ];
  }
} catch (error) {
  console.error("Error loading OpenAPI specification:", error);

  /**
   * Fallback OpenAPI specification
   * Provides minimal specification if the main file cannot be loaded
   */
  swaggerDocument = {
    openapi: "3.0.3",
    info: {
      title: "File Conversion Utility API",
      version: "1.0.0",
      description:
        "Enterprise-friendly file conversion API using pure Node.js libraries",
    },
    paths: {},
  };
}

/**
 * Swagger UI Configuration Options
 * Customizes the appearance and behavior of the interactive documentation
 */
const swaggerUiOptions = {
  explorer: true, // Enable the explorer panel
  swaggerOptions: {
    docExpansion: "list", // Expand operations list by default
    filter: true, // Enable filtering of operations
    showRequestDuration: true, // Show request duration in responses
    tryItOutEnabled: true, // Enable "Try it out" functionality
    requestInterceptor: (req) => {
      // Custom request interceptor for modifying requests before sending
      // Currently passes requests through unchanged
      return req;
    },
  },
  customCss: `
    .swagger-ui .topbar { display: none; }
    .swagger-ui .info { margin: 20px 0; }
    .swagger-ui .info .title { color: #2c3e50; }
    .swagger-ui .scheme-container { background: #f8f9fa; padding: 10px; border-radius: 5px; }
  `,
  customSiteTitle: "File Conversion API Documentation",
  customfavIcon: "/favicon.ico",
};

/**
 * Export Swagger configuration and setup function
 */
module.exports = {
  swaggerDocument,
  swaggerUi,
  swaggerUiOptions,

  /**
   * Setup Swagger documentation in Express application
   * Configures routes for interactive documentation and raw specification access
   *
   * @param {Object} app - Express application instance
   */
  setupSwagger: (app) => {
    /**
     * Serve interactive Swagger UI at /api-docs endpoint
     * Provides a web interface for exploring and testing the API
     * Uses CDN assets for better serverless compatibility
     */
    app.get("/api-docs", (req, res) => {
      const html = `
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>File Conversion API Documentation</title>
  <link rel="stylesheet" type="text/css" href="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5.0.0/swagger-ui.css" />
  <style>
    .swagger-ui .topbar { display: none; }
    .swagger-ui .info { margin: 20px 0; }
    .swagger-ui .info .title { color: #2c3e50; }
    .swagger-ui .scheme-container { background: #f8f9fa; padding: 10px; border-radius: 5px; }
  </style>
</head>
<body>
  <div id="swagger-ui"></div>
  <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5.0.0/swagger-ui-bundle.js"></script>
  <script src="https://cdn.jsdelivr.net/npm/swagger-ui-dist@5.0.0/swagger-ui-standalone-preset.js"></script>
  <script>
    window.onload = function() {
      const ui = SwaggerUIBundle({
        url: '/api-docs.json',
        dom_id: '#swagger-ui',
        deepLinking: true,
        presets: [
          SwaggerUIBundle.presets.apis,
          SwaggerUIStandalonePreset
        ],
        plugins: [
          SwaggerUIBundle.plugins.DownloadUrl
        ],
        layout: "StandaloneLayout",
        docExpansion: "list",
        filter: true,
        showRequestDuration: true,
        tryItOutEnabled: true
      });
    };
  </script>
</body>
</html>`;
      res.setHeader("Content-Type", "text/html");
      res.send(html);
    });

    app.get("/api-docs/", (req, res) => {
      res.redirect("/api-docs");
    });

    /**
     * Serve raw OpenAPI specification in JSON format
     * Useful for API clients and code generation tools
     */
    app.get("/api-docs.json", (req, res) => {
      res.setHeader("Content-Type", "application/json");
      res.send(swaggerDocument);
    });

    /**
     * Serve raw OpenAPI specification in YAML format
     * Alternative format for the specification file
     */
    app.get("/api-docs.yaml", (req, res) => {
      res.setHeader("Content-Type", "text/yaml");
      res.send(yaml.dump(swaggerDocument));
    });

    // Log available documentation endpoints
    console.log("Swagger documentation available at:");
    console.log(`  UI: http://localhost:${process.env.PORT || 3000}/api-docs`);
    console.log(
      `  JSON: http://localhost:${process.env.PORT || 3000}/api-docs.json`
    );
    console.log(
      `  YAML: http://localhost:${process.env.PORT || 3000}/api-docs.yaml`
    );
  },
};
