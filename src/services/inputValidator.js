/**
 * Input Validation Service
 *
 * Comprehensive input validation and sanitization service.
 * Provides schema-based validation, boundary checking, and malicious input detection.
 *
 * Features:
 * - Schema-based input validation
 * - Boundary value testing
 * - Malicious input detection
 * - Type coercion and sanitization
 * - Input normalization
 * - Validation metrics and monitoring
 */

class InputValidator {
  // Validation severity levels
  static VALIDATION_LEVELS = {
    PERMISSIVE: "PERMISSIVE", // Allow most inputs, basic validation
    STANDARD: "STANDARD", // Balanced validation and security
    STRICT: "STRICT", // High security, restrictive validation
    PARANOID: "PARANOID", // Maximum security, minimal acceptance
  };

  // Input types and their validation schemas
  static INPUT_SCHEMAS = {
    fileConversionRequest: {
      targetFormat: {
        type: "enum",
        values: ["pdf", "docx", "xlsx", "csv", "txt", "xml", "rtf"],
        required: true,
      },
      options: {
        type: "object",
        properties: {
          quality: {
            type: "enum",
            values: ["draft", "standard", "high", "ultra"],
            default: "standard",
          },
          preprocessing: { type: "boolean", default: true },
          timeout: { type: "number", min: 30000, max: 600000, default: 300000 },
        },
      },
    },
    filename: {
      type: "string",
      pattern: /^[\w\-. ]{1,255}(\.[a-zA-Z0-9]{1,10})?$/,
      maxLength: 255,
      required: true,
    },
    ipAddress: {
      type: "string",
      pattern:
        /^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$|^(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$|^::1$|^::$/,
      required: false,
    },
    email: {
      type: "string",
      pattern: /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/,
      maxLength: 254,
      required: false,
    },
    url: {
      type: "string",
      pattern:
        /^https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$/,
      maxLength: 2048,
      required: false,
    },
  };

  // Validation metrics
  static metrics = {
    totalValidations: 0,
    passedValidations: 0,
    failedValidations: 0,
    validationErrors: {},
    recentValidations: [],
  };

  // Current validation level
  static currentLevel = this.VALIDATION_LEVELS.STANDARD;

  /**
   * Set validation level for all operations
   * @param {string} level - Validation level
   */
  static setValidationLevel(level) {
    if (Object.values(this.VALIDATION_LEVELS).includes(level)) {
      this.currentLevel = level;
      console.log(`Input validation level set to: ${level}`);
    } else {
      throw new Error(`Invalid validation level: ${level}`);
    }
  }

  /**
   * Validate file conversion request
   * @param {Object} request - Request object to validate
   * @returns {Object} Validation result
   */
  static validateFileConversionRequest(request) {
    return this.validateAgainstSchema(
      request,
      this.INPUT_SCHEMAS.fileConversionRequest,
      "fileConversionRequest"
    );
  }

  /**
   * Validate filename
   * @param {string} filename - Filename to validate
   * @returns {Object} Validation result
   */
  static validateFilename(filename) {
    return this.validateValue(
      filename,
      this.INPUT_SCHEMAS.filename,
      "filename"
    );
  }

  /**
   * Validate IP address
   * @param {string} ip - IP address to validate
   * @returns {Object} Validation result
   */
  static validateIPAddress(ip) {
    return this.validateValue(ip, this.INPUT_SCHEMAS.ipAddress, "ipAddress");
  }

  /**
   * Validate email address
   * @param {string} email - Email to validate
   * @returns {Object} Validation result
   */
  static validateEmail(email) {
    return this.validateValue(email, this.INPUT_SCHEMAS.email, "email");
  }

  /**
   * Validate URL
   * @param {string} url - URL to validate
   * @returns {Object} Validation result
   */
  static validateURL(url) {
    return this.validateValue(url, this.INPUT_SCHEMAS.url, "url");
  }

  /**
   * Validate input against schema
   * @param {any} input - Input to validate
   * @param {Object} schema - Validation schema
   * @param {string} schemaName - Name of the schema for logging
   * @returns {Object} Validation result
   */
  static validateAgainstSchema(input, schema, schemaName = "unknown") {
    this.metrics.totalValidations++;

    try {
      const result = this._validateObject(input, schema);

      if (result.valid) {
        this.metrics.passedValidations++;
        this._recordValidation("PASS", schemaName, input, result);
        return result;
      } else {
        this.metrics.failedValidations++;
        this._recordValidation("FAIL", schemaName, input, result);
        return result;
      }
    } catch (error) {
      this.metrics.failedValidations++;
      const result = {
        valid: false,
        errors: [`Validation system error: ${error.message}`],
        sanitized: input,
      };
      this._recordValidation("ERROR", schemaName, input, result);
      return result;
    }
  }

  /**
   * Validate single value against schema
   * @param {any} value - Value to validate
   * @param {Object} schema - Validation schema
   * @param {string} fieldName - Name of the field for logging
   * @returns {Object} Validation result
   */
  static validateValue(value, schema, fieldName = "unknown") {
    this.metrics.totalValidations++;

    try {
      const result = this._validateValue(value, schema);

      if (result.valid) {
        this.metrics.passedValidations++;
        this._recordValidation("PASS", fieldName, value, result);
        return result;
      } else {
        this.metrics.failedValidations++;
        this._recordValidation("FAIL", fieldName, value, result);
        return result;
      }
    } catch (error) {
      this.metrics.failedValidations++;
      const result = {
        valid: false,
        errors: [`Validation error: ${error.message}`],
        sanitized: value,
      };
      this._recordValidation("ERROR", fieldName, value, result);
      return result;
    }
  }

  /**
   * Validate object against schema
   * @param {Object} obj - Object to validate
   * @param {Object} schema - Schema to validate against
   * @returns {Object} Validation result
   */
  static _validateObject(obj, schema) {
    if (!obj || typeof obj !== "object") {
      return {
        valid: false,
        errors: ["Input must be an object"],
        sanitized: {},
      };
    }

    const errors = [];
    const sanitized = {};
    let valid = true;

    // Validate each property in schema
    for (const [key, rules] of Object.entries(schema)) {
      const value = obj[key];

      if (
        rules.required &&
        (value === undefined || value === null || value === "")
      ) {
        errors.push(`${key} is required`);
        valid = false;
        continue;
      }

      if (value !== undefined && value !== null && value !== "") {
        const fieldResult = this._validateValue(value, rules);
        if (!fieldResult.valid) {
          errors.push(...fieldResult.errors.map((err) => `${key}: ${err}`));
          valid = false;
        } else {
          sanitized[key] = fieldResult.sanitized;
        }
      } else if (rules.default !== undefined) {
        sanitized[key] = rules.default;
      }
    }

    return { valid, errors, sanitized };
  }

  /**
   * Validate single value against rules
   * @param {any} value - Value to validate
   * @param {Object} rules - Validation rules
   * @returns {Object} Validation result
   */
  static _validateValue(value, rules) {
    const errors = [];

    // Type validation
    switch (rules.type) {
      case "string":
        if (typeof value !== "string") {
          errors.push("Must be a string");
          return { valid: false, errors, sanitized: String(value) };
        }

        // Length validation
        if (rules.maxLength && value.length > rules.maxLength) {
          errors.push(`Must be at most ${rules.maxLength} characters`);
        }

        // Pattern validation
        if (rules.pattern && !rules.pattern.test(value)) {
          errors.push("Format is invalid");
        }

        // Sanitize string
        let sanitized = this._sanitizeString(value, rules);
        return {
          valid: errors.length === 0,
          errors,
          sanitized,
        };

      case "number":
        const numValue = typeof value === "string" ? parseFloat(value) : value;
        if (isNaN(numValue)) {
          errors.push("Must be a valid number");
          return { valid: false, errors, sanitized: 0 };
        }

        if (rules.min !== undefined && numValue < rules.min) {
          errors.push(`Must be at least ${rules.min}`);
        }

        if (rules.max !== undefined && numValue > rules.max) {
          errors.push(`Must be at most ${rules.max}`);
        }

        return {
          valid: errors.length === 0,
          errors,
          sanitized: numValue,
        };

      case "boolean":
        const boolValue =
          typeof value === "string"
            ? value.toLowerCase() === "true"
            : Boolean(value);
        return {
          valid: true,
          errors: [],
          sanitized: boolValue,
        };

      case "enum":
        if (!rules.values.includes(value)) {
          errors.push(`Must be one of: ${rules.values.join(", ")}`);
        }

        return {
          valid: errors.length === 0,
          errors,
          sanitized: value,
        };

      case "object":
        if (rules.properties) {
          return this._validateObject(value, rules.properties);
        }
        return {
          valid: true,
          errors: [],
          sanitized: value,
        };

      default:
        return {
          valid: false,
          errors: [`Unknown validation type: ${rules.type}`],
          sanitized: value,
        };
    }
  }

  /**
   * Sanitize string input
   * @param {string} str - String to sanitize
   * @param {Object} rules - Validation rules
   * @returns {string} Sanitized string
   */
  static _sanitizeString(str, rules) {
    let sanitized = str;

    // Trim whitespace
    sanitized = sanitized.trim();

    // Remove null bytes and control characters
    sanitized = sanitized.replace(/[\x00-\x1F\x7F-\x9F]/g, "");

    // Apply length limits
    if (rules.maxLength && sanitized.length > rules.maxLength) {
      sanitized = sanitized.substring(0, rules.maxLength);
    }

    // HTML entity encoding for security
    sanitized = sanitized
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#x27;");

    // Additional security sanitization based on validation level
    if (
      this.currentLevel === this.VALIDATION_LEVELS.STRICT ||
      this.currentLevel === this.VALIDATION_LEVELS.PARANOID
    ) {
      // Remove potentially dangerous characters
      sanitized = sanitized.replace(/[<>""'&]/g, "");
    }

    if (this.currentLevel === this.VALIDATION_LEVELS.PARANOID) {
      // Extremely restrictive - only alphanumeric, spaces, and basic punctuation
      sanitized = sanitized.replace(/[^a-zA-Z0-9\s\.,!?\-]/g, "");
    }

    return sanitized;
  }

  /**
   * Record validation result for metrics
   * @param {string} status - Validation status (PASS/FAIL/ERROR)
   * @param {string} fieldName - Name of field/schema
   * @param {any} input - Original input
   * @param {Object} result - Validation result
   */
  static _recordValidation(status, fieldName, input, result) {
    const record = {
      timestamp: new Date().toISOString(),
      status,
      fieldName,
      input: typeof input === "object" ? JSON.stringify(input) : String(input),
      errors: result.errors || [],
      level: this.currentLevel,
    };

    this.metrics.recentValidations.unshift(record);
    if (this.metrics.recentValidations.length > 100) {
      this.metrics.recentValidations.pop();
    }

    // Track error types
    if (result.errors && result.errors.length > 0) {
      result.errors.forEach((error) => {
        const errorKey = `${fieldName}:${error}`;
        this.metrics.validationErrors[errorKey] =
          (this.metrics.validationErrors[errorKey] || 0) + 1;
      });
    }

    // Log validation failures for monitoring
    if (status === "FAIL") {
      console.warn(`Input validation failed for ${fieldName}:`, result.errors);
    }
  }

  /**
   * Get validation metrics
   * @returns {Object} Validation metrics
   */
  static getValidationMetrics() {
    const successRate =
      this.metrics.totalValidations > 0
        ? (this.metrics.passedValidations / this.metrics.totalValidations) * 100
        : 0;

    return {
      timestamp: new Date().toISOString(),
      totalValidations: this.metrics.totalValidations,
      passedValidations: this.metrics.passedValidations,
      failedValidations: this.metrics.failedValidations,
      successRate: Math.round(successRate * 100) / 100,
      validationErrors: this.metrics.validationErrors,
      recentValidations: this.metrics.recentValidations.slice(0, 10),
      currentLevel: this.currentLevel,
      summary: {
        mostCommonErrors: Object.entries(this.metrics.validationErrors)
          .sort(([, a], [, b]) => b - a)
          .slice(0, 5)
          .map(([error, count]) => ({ error, count })),
      },
    };
  }

  /**
   * Reset validation metrics
   */
  static resetMetrics() {
    this.metrics = {
      totalValidations: 0,
      passedValidations: 0,
      failedValidations: 0,
      validationErrors: {},
      recentValidations: [],
    };
  }

  /**
   * Comprehensive input sanitization for any input
   * @param {any} input - Input to sanitize
   * @returns {any} Sanitized input
   */
  static sanitizeInput(input) {
    if (typeof input === "string") {
      return this._sanitizeString(input, { maxLength: 10000 });
    } else if (typeof input === "object" && input !== null) {
      // Recursively sanitize objects
      const sanitized = Array.isArray(input) ? [] : {};
      for (const [key, value] of Object.entries(input)) {
        sanitized[key] = this.sanitizeInput(value);
      }
      return sanitized;
    } else {
      return input;
    }
  }
}

module.exports = InputValidator;
