/**
 * Swagger/OpenAPI Documentation Configuration
 *
 * Configures and sets up interactive API documentation using Swagger UI
 * Loads the OpenAPI specification from YAML file and provides endpoints
 */

const yaml = require("js-yaml");
const fs = require("fs");
const path = require("path");

/**
 * Load and parse OpenAPI specification
 */
let swaggerDocument;
try {
  swaggerDocument = yaml.load(
    fs.readFileSync(path.join(__dirname, "../../docs/openapi.yaml"), "utf8")
  );

  // Always use a relative path so Swagger "Try it out" works in any env
  swaggerDocument.servers = [
    { url: "/api", description: "Current environment" },
  ];
} catch (error) {
  console.error("Error loading OpenAPI specification:", error);

  // Fallback OpenAPI specification
  swaggerDocument = {
    openapi: "3.0.3",
    info: {
      title: "File Conversion Utility API",
      version: "1.0.0",
      description: "File conversion API using pure Node.js libraries",
    },
    paths: {},
  };
}

/**
 * Setup Swagger documentation in Express application
 * Serves all assets locally - no CDN dependencies
 */
function setupSwagger(app) {
  // Serve Swagger UI static assets from local node_modules
  const swaggerUiPath = path.join(
    __dirname,
    "../../node_modules/swagger-ui-dist"
  );
  app.use("/swagger-ui", require("express").static(swaggerUiPath));

  // Serve interactive Swagger UI at /api-docs endpoint
  app.get("/api-docs", (req, res) => {
    const html = `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>File Conversion API Documentation</title>
    <link rel="stylesheet" type="text/css" href="/swagger-ui/swagger-ui.css" />
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }
        .swagger-ui .topbar { display: none; }
        .swagger-ui .info { margin: 20px 0; }
        .swagger-ui .info .title { color: #2c3e50; }
        .swagger-ui .scheme-container { background: #f8f9fa; padding: 10px; border-radius: 5px; }
    </style>
</head>
<body>
    <div id="swagger-ui"></div>
    <script src="/swagger-ui/swagger-ui-bundle.js"></script>
    <script src="/swagger-ui/swagger-ui-standalone-preset.js"></script>
    <script>
        window.onload = function() {
            const ui = SwaggerUIBundle({
                url: '/api-docs.json',
                dom_id: '#swagger-ui',
                deepLinking: true,
                presets: [SwaggerUIBundle.presets.apis, SwaggerUIStandalonePreset],
                plugins: [SwaggerUIBundle.plugins.DownloadUrl],
                layout: "BaseLayout"
            });
        };
    </script>
</body>
</html>`;

    res.send(html);
  });

  // Serve OpenAPI specification in JSON format
  app.get("/api-docs.json", (req, res) => {
    res.json(swaggerDocument);
  });

  // Serve OpenAPI specification in YAML format
  app.get("/api-docs.yaml", (req, res) => {
    res.setHeader("Content-Type", "text/yaml");
    res.send(yaml.dump(swaggerDocument));
  });

  console.log("Swagger documentation available at:");
  console.log("  UI: /api-docs");
  console.log("  JSON: /api-docs.json");
  console.log("  YAML: /api-docs.yaml");
}

module.exports = { setupSwagger };
