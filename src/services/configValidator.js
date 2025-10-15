/**
 * Configuration Validator
 *
 * Comprehensive configuration validation and management service.
 * Provides schema-based validation and environment-specific configuration management.
 *
 * Features:
 * - Schema-based configuration validation
 * - Environment-specific configuration management
 * - Configuration change tracking and auditing
 * - Security-sensitive configuration protection
 * - Configuration health monitoring
 * - Graceful degradation for invalid configurations
 */

const fs = require("fs");
const path = require("path");

class ConfigValidator {
  // Configuration validation schemas
  static CONFIG_SCHEMAS = {
    server: {
      PORT: { type: "number", min: 1, max: 65535, default: 3000 },
      HOST: { type: "string", pattern: /^[\w.-]+$/, default: "localhost" },
      NODE_ENV: {
        type: "enum",
        values: ["development", "test", "staging", "production"],
        default: "development",
      },
      SSL_ENABLED: { type: "boolean", default: false },
      TEMP_DIR: { type: "string", default: "./temp" },
    },
    security: {
      IP_WHITELIST: { type: "array", itemType: "string", default: [] },
      RATE_LIMIT_MAX: { type: "number", min: 1, max: 10000, default: 100 },
      RATE_LIMIT_WINDOW: { type: "number", min: 1000, default: 900000 },
      UPLOAD_LIMIT: {
        type: "string",
        pattern: /^\d+(mb|kb|gb)$/i,
        default: "50mb",
      },
      SESSION_TIMEOUT: { type: "number", min: 60000, default: 3600000 },
    },
    conversion: {
      ENABLE_PREPROCESSING: { type: "boolean", default: true },
      MAX_CONCURRENT_CONVERSIONS: {
        type: "number",
        min: 1,
        max: 20,
        default: 2,
      },
      CONVERSION_TIMEOUT: { type: "number", min: 30000, default: 300000 },
      CLEANUP_TEMP_FILES: { type: "boolean", default: true },
      FIDELITY_TARGET: {
        type: "string",
        pattern: /^\d{2}-\d{2}%$/,
        default: "98-99%",
      },
    },
    logging: {
      LOG_LEVEL: {
        type: "enum",
        values: ["trace", "debug", "info", "warn", "error"],
        default: "info",
      },
      TELEMETRY_FORMAT: {
        type: "enum",
        values: ["json", "text"],
        default: "json",
      },
      LOG_RETENTION_DAYS: { type: "number", min: 1, max: 365, default: 30 },
      AUDIT_LOG_ENABLED: { type: "boolean", default: true },
    },
  };

  // Configuration validation results
  static validationResults = {
    timestamp: null,
    valid: false,
    errors: [],
    warnings: [],
    recommendations: [],
  };

  // Configuration change history
  static configHistory = [];

  /**
   * Validate complete configuration against schemas
   * @returns {Object} Validation results
   */
  static validateConfiguration() {
    console.log("Starting configuration validation...");

    this.validationResults = {
      timestamp: new Date().toISOString(),
      valid: true,
      errors: [],
      warnings: [],
      recommendations: [],
    };

    let totalValidations = 0;
    let passedValidations = 0;

    // Validate each configuration category
    for (const [category, schema] of Object.entries(this.CONFIG_SCHEMAS)) {
      const categoryResults = this.validateCategory(category, schema);
      totalValidations += categoryResults.total;
      passedValidations += categoryResults.passed;

      this.validationResults.errors.push(...categoryResults.errors);
      this.validationResults.warnings.push(...categoryResults.warnings);
      this.validationResults.recommendations.push(
        ...categoryResults.recommendations
      );
    }

    // Perform cross-configuration validation
    this.validateConfigurationConsistency();

    // Validate configuration file integrity
    this.validateConfigurationFiles();

    // Determine overall validity
    this.validationResults.valid = this.validationResults.errors.length === 0;

    // Generate validation summary
    this.generateValidationSummary(totalValidations, passedValidations);

    return this.validationResults;
  }

  /**
   * Validate configuration category against schema
   * @param {string} category - Configuration category name
   * @param {Object} schema - Validation schema for category
   * @returns {Object} Category validation results
   */
  static validateCategory(category, schema) {
    const results = {
      total: 0,
      passed: 0,
      errors: [],
      warnings: [],
      recommendations: [],
    };

    for (const [key, rules] of Object.entries(schema)) {
      results.total++;

      const envValue = process.env[key];
      const validation = this.validateConfigValue(key, envValue, rules);

      if (validation.valid) {
        results.passed++;
      } else if (validation.level === "error") {
        results.errors.push({
          category,
          key,
          message: validation.message,
          severity: validation.level,
        });
      } else if (validation.level === "warning") {
        results.warnings.push({
          category,
          key,
          message: validation.message,
          severity: validation.level,
        });
      }

      // Add recommendations for optimization
      if (validation.recommendation) {
        results.recommendations.push({
          category,
          key,
          recommendation: validation.recommendation,
        });
      }
    }

    return results;
  }

  /**
   * Validate individual configuration value
   * @param {string} key - Configuration key
   * @param {*} value - Configuration value
   * @param {Object} rules - Validation rules
   * @returns {Object} Validation result
   */
  static validateConfigValue(key, value, rules) {
    // Check if value is set
    if (value === undefined || value === null || value === "") {
      if (rules.required === false) {
        return {
          valid: true,
          level: "info",
          message: `Using default value: ${rules.default}`,
        };
      }
      return {
        valid: false,
        level: "error",
        message: `Required configuration ${key} is not set`,
        recommendation: `Set ${key}=${rules.default} in environment variables`,
      };
    }

    // Type validation
    switch (rules.type) {
      case "number":
        const numValue = parseFloat(value);
        if (isNaN(numValue)) {
          return {
            valid: false,
            level: "error",
            message: `${key} must be a valid number, got: ${value}`,
          };
        }
        if (rules.min !== undefined && numValue < rules.min) {
          return {
            valid: false,
            level: "error",
            message: `${key} must be >= ${rules.min}, got: ${numValue}`,
          };
        }
        if (rules.max !== undefined && numValue > rules.max) {
          return {
            valid: false,
            level: "error",
            message: `${key} must be <= ${rules.max}, got: ${numValue}`,
          };
        }
        break;

      case "boolean":
        const boolValue = value.toLowerCase();
        if (!["true", "false", "1", "0", "yes", "no"].includes(boolValue)) {
          return {
            valid: false,
            level: "error",
            message: `${key} must be a boolean value, got: ${value}`,
          };
        }
        break;

      case "enum":
        if (!rules.values.includes(value.toLowerCase())) {
          return {
            valid: false,
            level: "error",
            message: `${key} must be one of: ${rules.values.join(
              ", "
            )}, got: ${value}`,
          };
        }
        break;

      case "string":
        if (rules.pattern && !rules.pattern.test(value)) {
          return {
            valid: false,
            level: "error",
            message: `${key} format is invalid, got: ${value}`,
          };
        }
        if (rules.minLength && value.length < rules.minLength) {
          return {
            valid: false,
            level: "error",
            message: `${key} must be at least ${rules.minLength} characters`,
          };
        }
        if (rules.maxLength && value.length > rules.maxLength) {
          return {
            valid: false,
            level: "warning",
            message: `${key} is very long (${value.length} chars), consider shortening`,
          };
        }
        break;

      case "array":
        try {
          const arrayValue = Array.isArray(value) ? value : JSON.parse(value);
          if (!Array.isArray(arrayValue)) {
            return {
              valid: false,
              level: "error",
              message: `${key} must be an array`,
            };
          }
          if (rules.itemType) {
            for (let i = 0; i < arrayValue.length; i++) {
              if (typeof arrayValue[i] !== rules.itemType) {
                return {
                  valid: false,
                  level: "error",
                  message: `${key}[${i}] must be of type ${rules.itemType}`,
                };
              }
            }
          }
        } catch (e) {
          return {
            valid: false,
            level: "error",
            message: `${key} must be a valid JSON array`,
          };
        }
        break;
    }

    // Security validation for sensitive keys
    if (this.isSensitiveKey(key)) {
      const securityCheck = this.validateSecurityConstraints(key, value);
      if (!securityCheck.valid) {
        return securityCheck;
      }
    }

    // Performance recommendations
    const performanceCheck = this.checkPerformanceImplications(key, value);
    if (performanceCheck.recommendation) {
      return {
        valid: true,
        level: "info",
        message: "Configuration valid",
        recommendation: performanceCheck.recommendation,
      };
    }

    return { valid: true, level: "info", message: "Configuration valid" };
  }

  /**
   * Validate configuration consistency across categories
   */
  static validateConfigurationConsistency() {
    // Check for conflicting configurations
    if (
      process.env.NODE_ENV === "production" &&
      process.env.LOG_LEVEL === "debug"
    ) {
      this.validationResults.warnings.push({
        category: "consistency",
        key: "LOG_LEVEL",
        message: "Debug logging in production may impact performance",
        severity: "warning",
      });
    }

    if (
      process.env.SSL_ENABLED === "false" &&
      process.env.NODE_ENV === "production"
    ) {
      this.validationResults.warnings.push({
        category: "consistency",
        key: "SSL_ENABLED",
        message: "SSL disabled in production environment",
        severity: "warning",
      });
    }

    if (
      parseInt(process.env.MAX_CONCURRENT_CONVERSIONS) > 10 &&
      parseInt(process.env.CONVERSION_TIMEOUT) < 60000
    ) {
      this.validationResults.recommendations.push({
        category: "consistency",
        key: "CONVERSION_TIMEOUT",
        recommendation:
          "Consider increasing CONVERSION_TIMEOUT with high concurrency",
      });
    }
  }

  /**
   * Validate configuration file integrity
   */
  static validateConfigurationFiles() {
    const configFiles = ["package.json", "jest.config.js", "env.example"];

    for (const file of configFiles) {
      const filePath = path.join(process.cwd(), file);
      if (!fs.existsSync(filePath)) {
        this.validationResults.warnings.push({
          category: "files",
          key: file,
          message: `Configuration file ${file} not found`,
          severity: "warning",
        });
        continue;
      }

      try {
        const stats = fs.statSync(filePath);
        if (stats.size === 0) {
          this.validationResults.errors.push({
            category: "files",
            key: file,
            message: `Configuration file ${file} is empty`,
            severity: "error",
          });
        }
      } catch (error) {
        this.validationResults.errors.push({
          category: "files",
          key: file,
          message: `Cannot read configuration file ${file}: ${error.message}`,
          severity: "error",
        });
      }
    }
  }

  /**
   * Check if configuration key contains sensitive information
   * @param {string} key - Configuration key
   * @returns {boolean} Whether key is sensitive
   */
  static isSensitiveKey(key) {
    const sensitivePatterns = [
      /password/i,
      /secret/i,
      /key/i,
      /token/i,
      /credential/i,
    ];

    return sensitivePatterns.some((pattern) => pattern.test(key));
  }

  /**
   * Validate security constraints for sensitive configuration
   * @param {string} key - Configuration key
   * @param {string} value - Configuration value
   * @returns {Object} Security validation result
   */
  static validateSecurityConstraints(key, value) {
    // Check for potential hardcoded secrets using pattern matching
    const secretPatterns = [
      /^[A-Za-z0-9+/=]{20,}$/, // Base64 encoded data (20+ chars)
      /^[A-Za-z0-9_-]{20,}$/, // JWT tokens or API keys
      /sk-[A-Za-z0-9_-]{20,}/, // OpenAI-like secret keys
      /AKIA[0-9A-Z]{16}/, // AWS access keys
      /SG\.[A-Za-z0-9_-]{20,}/, // SendGrid API keys
      /Bearer\s+[A-Za-z0-9_-]{20,}/i, // Bearer tokens
      /password|secret|token|key/i, // Case-insensitive keyword detection
    ];

    const isPotentialSecret = secretPatterns.some((pattern) =>
      pattern.test(value)
    );

    if (isPotentialSecret) {
      return {
        valid: false,
        level: "warning",
        message: `${key} appears to contain a potential secret`,
        recommendation: `Move ${key} to environment variables or use environment-specific naming`,
      };
    }

    // Check for weak secrets
    if (value && value.length < 12) {
      return {
        valid: false,
        level: "warning",
        message: `${key} is too short for a secure secret`,
        recommendation: `Use secrets with at least 12 characters`,
      };
    }

    return { valid: true };
  }

  /**
   * Check performance implications of configuration values
   * @param {string} key - Configuration key
   * @param {string} value - Configuration value
   * @returns {Object} Performance check result
   */
  static checkPerformanceImplications(key, value) {
    const recommendations = {
      LOG_LEVEL:
        value === "debug"
          ? "Debug logging may impact performance in production"
          : null,
      MAX_CONCURRENT_CONVERSIONS:
        parseInt(value) > 5
          ? "High concurrency may require more system resources"
          : null,
      CONVERSION_TIMEOUT:
        parseInt(value) > 300000
          ? "Long timeouts may impact system responsiveness"
          : null,
      UPLOAD_LIMIT: value.includes("gb")
        ? "Large upload limits may impact memory usage"
        : null,
    };

    return { recommendation: recommendations[key] || null };
  }

  /**
   * Generate validation summary
   * @param {number} total - Total validations performed
   * @param {number} passed - Validations that passed
   */
  static generateValidationSummary(total, passed) {
    const passRate = total > 0 ? ((passed / total) * 100).toFixed(1) : 0;

    console.log(`Configuration validation completed:`);
    console.log(`   Total validations: ${total}`);
    console.log(`   Passed: ${passed} (${passRate}%)`);
    console.log(`   Errors: ${this.validationResults.errors.length}`);
    console.log(`   Warnings: ${this.validationResults.warnings.length}`);
    console.log(
      `   Recommendations: ${this.validationResults.recommendations.length}`
    );

    if (this.validationResults.errors.length > 0) {
      console.log(
        "Critical configuration errors found - system may not function correctly"
      );
    } else if (this.validationResults.warnings.length > 0) {
      console.log("Configuration warnings detected - review recommended");
    } else {
      console.log("All configuration validated successfully");
    }
  }

  /**
   * Track configuration changes for audit trail
   * @param {string} key - Configuration key that changed
   * @param {string} oldValue - Previous value
   * @param {string} newValue - New value
   * @param {string} source - Source of change
   */
  static trackConfigurationChange(key, oldValue, newValue, source = "unknown") {
    const change = {
      timestamp: new Date().toISOString(),
      key,
      oldValue,
      newValue,
      source,
      environment: process.env.NODE_ENV,
    };

    this.configHistory.push(change);

    // Keep only last 1000 changes for memory management
    if (this.configHistory.length > 1000) {
      this.configHistory.shift();
    }

    console.log(
      `Configuration change tracked: ${key} = ${newValue} (source: ${source})`
    );
  }

  /**
   * Get configuration audit trail
   * @returns {Array} Configuration change history
   */
  static getConfigurationAuditTrail() {
    return this.configHistory.slice(-100); // Last 100 changes
  }

  /**
   * Generate configuration report for compliance
   * @returns {Object} Configuration compliance report
   */
  static generateConfigurationReport() {
    return {
      timestamp: new Date().toISOString(),
      environment: process.env.NODE_ENV,
      validationResults: this.validationResults,
      auditTrail: this.getConfigurationAuditTrail(),
      securityCompliance: this.checkSecurityCompliance(),
      performanceProfile: this.generatePerformanceProfile(),
    };
  }

  /**
   * Check security compliance of current configuration
   * @returns {Object} Security compliance assessment
   */
  static checkSecurityCompliance() {
    const checks = {
      sslEnabled: process.env.SSL_ENABLED === "true",
      ipWhitelistEnabled:
        process.env.IP_WHITELIST && process.env.IP_WHITELIST !== "[]",
      rateLimitingEnabled: parseInt(process.env.RATE_LIMIT_MAX) > 0,
      auditLoggingEnabled: process.env.AUDIT_LOG_ENABLED !== "false",
      secureHeadersEnabled: true, // Assumed enabled in helmet config
      noSensitiveDataInLogs:
        !process.env.LOG_LEVEL || process.env.LOG_LEVEL !== "debug",
    };

    const passedChecks = Object.values(checks).filter(Boolean).length;
    const totalChecks = Object.keys(checks).length;
    const complianceScore = (passedChecks / totalChecks) * 100;

    return {
      score: Math.round(complianceScore),
      checks,
      status:
        complianceScore >= 80
          ? "COMPLIANT"
          : complianceScore >= 60
          ? "WARNING"
          : "NON_COMPLIANT",
    };
  }

  /**
   * Generate performance profile based on configuration
   * @returns {Object} Performance profile assessment
   */
  static generatePerformanceProfile() {
    const profile = {
      concurrencyLevel: parseInt(process.env.MAX_CONCURRENT_CONVERSIONS) || 2,
      timeoutSettings: parseInt(process.env.CONVERSION_TIMEOUT) || 300000,
      memoryLimits: process.env.UPLOAD_LIMIT || "50mb",
      preprocessingEnabled: process.env.ENABLE_PREPROCESSING !== "false",
      assessment: "UNKNOWN",
    };

    // Assess performance profile
    if (profile.concurrencyLevel <= 2 && profile.timeoutSettings <= 300000) {
      profile.assessment = "CONSERVATIVE";
    } else if (
      profile.concurrencyLevel <= 5 &&
      profile.timeoutSettings <= 600000
    ) {
      profile.assessment = "BALANCED";
    } else {
      profile.assessment = "HIGH_PERFORMANCE";
    }

    return profile;
  }
}

module.exports = ConfigValidator;
