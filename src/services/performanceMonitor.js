/**
 * Performance Monitor
 *
 * Monitors system performance, detects issues, and provides analytics.
 * Implements NASA/JPL standards for mission-critical applications.
 */

const os = require("os");

class PerformanceMonitor {
  // Performance monitoring thresholds (NASA/JPL recommended values)
  static THRESHOLDS = {
    memoryUsagePercent: 80, // Alert when memory usage > 80%
    cpuUsagePercent: 70, // Alert when CPU usage > 70%
    responseTimeMs: 5000, // Alert when response time > 5 seconds
    errorRatePercent: 5, // Alert when error rate > 5%
    throughputMin: 10, // Minimum acceptable throughput
    memoryLeakThreshold: 100 * 1024 * 1024, // 100MB growth over baseline
    conversionTimeoutMs: 300000, // 5 minutes max conversion time
  };

  // Performance metrics storage
  static metrics = {
    system: [],
    conversions: [],
    requests: [],
    alerts: [],
  };

  // Performance baselines for drift detection
  static baselines = {
    memoryUsage: null,
    cpuUsage: null,
    responseTime: null,
    throughput: null,
    lastUpdated: null,
  };

  // Active monitoring intervals
  static monitoringIntervals = new Map();

  /**
   * Initialize performance monitoring
   */
  static initialize() {
    console.log("Initializing NASA/JPL performance monitoring...");

    // Establish performance baselines
    this.establishBaselines();

    // Start continuous monitoring
    this.startContinuousMonitoring();

    // Set up periodic baseline updates
    this.monitoringIntervals.set(
      "baseline-update",
      setInterval(() => this.updateBaselines(), 3600000) // Update baselines hourly
    );

    console.log("Performance monitoring initialized");
  }

  /**
   * Establish performance baselines
   */
  static establishBaselines() {
    console.log("Establishing performance baselines...");

    // Collect baseline data over 30 seconds
    const baselineSamples = [];
    const sampleInterval = setInterval(() => {
      baselineSamples.push(this.collectSystemMetrics());
    }, 1000);

    setTimeout(() => {
      clearInterval(sampleInterval);

      if (baselineSamples.length > 0) {
        this.baselines.memoryUsage = this.calculateAverage(
          baselineSamples.map((s) => s.memoryUsagePercent)
        );
        this.baselines.cpuUsage = this.calculateAverage(
          baselineSamples.map((s) => s.cpuUsagePercent)
        );
        this.baselines.lastUpdated = new Date().toISOString();

        console.log("Performance baselines established");
        console.log(
          `   Memory baseline: ${this.baselines.memoryUsage.toFixed(1)}%`
        );
        console.log(`   CPU baseline: ${this.baselines.cpuUsage.toFixed(1)}%`);
      }
    }, 30000);
  }

  /**
   * Start continuous performance monitoring
   */
  static startContinuousMonitoring() {
    // System metrics collection every 5 seconds
    this.monitoringIntervals.set(
      "system-metrics",
      setInterval(() => this.collectAndAnalyzeSystemMetrics(), 5000)
    );

    // Performance analysis every minute
    this.monitoringIntervals.set(
      "performance-analysis",
      setInterval(() => this.performPerformanceAnalysis(), 60000)
    );

    // Memory leak detection every 10 minutes
    this.monitoringIntervals.set(
      "memory-leak-detection",
      setInterval(() => this.detectMemoryLeaks(), 600000)
    );
  }

  /**
   * Collect and analyze current system metrics
   */
  static collectAndAnalyzeSystemMetrics() {
    const metrics = this.collectSystemMetrics();

    // Store metrics
    this.metrics.system.push({
      ...metrics,
      timestamp: new Date(),
    });

    // Keep only last 1000 system metric samples
    if (this.metrics.system.length > 1000) {
      this.metrics.system.shift();
    }

    // Check for alerts
    this.checkSystemAlerts(metrics);
  }

  /**
   * Collect comprehensive system metrics
   * @returns {Object} System performance metrics
   */
  static collectSystemMetrics() {
    const memUsage = process.memoryUsage();
    const cpuUsage = process.cpuUsage();
    const loadAverage = os.loadavg();

    return {
      timestamp: new Date().toISOString(),
      memoryUsageBytes: memUsage.heapUsed,
      memoryUsagePercent: (memUsage.heapUsed / memUsage.heapTotal) * 100,
      memoryTotalBytes: memUsage.heapTotal,
      memoryExternalBytes: memUsage.external,
      cpuUsagePercent: (cpuUsage.user + cpuUsage.system) / 1000000, // Convert to percentage
      loadAverage1m: loadAverage[0],
      loadAverage5m: loadAverage[1],
      loadAverage15m: loadAverage[2],
      activeHandles: process._getActiveHandles
        ? process._getActiveHandles().length
        : 0,
      activeRequests: process._getActiveRequests
        ? process._getActiveRequests().length
        : 0,
      uptime: process.uptime(),
      freemem: os.freemem(),
      totalmem: os.totalmem(),
    };
  }

  /**
   * Check system metrics against thresholds and generate alerts
   * @param {Object} metrics - Current system metrics
   */
  static checkSystemAlerts(metrics) {
    const alerts = [];

    // Memory usage alert
    if (metrics.memoryUsagePercent > this.THRESHOLDS.memoryUsagePercent) {
      alerts.push({
        type: "MEMORY_USAGE_HIGH",
        severity: "WARNING",
        message: `Memory usage at ${metrics.memoryUsagePercent.toFixed(
          1
        )}% (threshold: ${this.THRESHOLDS.memoryUsagePercent}%)`,
        metrics,
        recommendation:
          "Consider increasing memory limits or optimizing memory usage",
      });
    }

    // CPU usage alert
    if (metrics.cpuUsagePercent > this.THRESHOLDS.cpuUsagePercent) {
      alerts.push({
        type: "CPU_USAGE_HIGH",
        severity: "WARNING",
        message: `CPU usage at ${metrics.cpuUsagePercent.toFixed(
          1
        )}% (threshold: ${this.THRESHOLDS.cpuUsagePercent}%)`,
        metrics,
        recommendation:
          "Monitor CPU-intensive operations and consider load balancing",
      });
    }

    // Memory leak detection
    if (
      this.baselines.memoryUsage &&
      metrics.memoryUsagePercent > this.baselines.memoryUsage + 20
    ) {
      alerts.push({
        type: "POTENTIAL_MEMORY_LEAK",
        severity: "CRITICAL",
        message: `Memory usage significantly above baseline: ${metrics.memoryUsagePercent.toFixed(
          1
        )}% vs ${this.baselines.memoryUsage.toFixed(1)}%`,
        metrics,
        recommendation:
          "Investigate for memory leaks, consider restarting the service",
      });
    }

    // High load average alert
    if (metrics.loadAverage1m > os.cpus().length) {
      alerts.push({
        type: "HIGH_LOAD_AVERAGE",
        severity: "WARNING",
        message: `System load average (1m): ${metrics.loadAverage1m.toFixed(
          2
        )} (CPU cores: ${os.cpus().length})`,
        metrics,
        recommendation:
          "System is overloaded, consider scaling or optimizing performance",
      });
    }

    // Generate alerts
    alerts.forEach((alert) => {
      this.generateAlert(alert);
    });
  }

  /**
   * Track conversion performance
   * @param {string} operation - Conversion operation type
   * @param {number} duration - Operation duration in milliseconds
   * @param {boolean} success - Operation success status
   * @param {Object} metadata - Additional conversion metadata
   */
  static trackConversionPerformance(
    operation,
    duration,
    success,
    metadata = {}
  ) {
    const conversionMetric = {
      operation,
      duration,
      success,
      timestamp: new Date(),
      memoryUsage: process.memoryUsage().heapUsed,
      cpuUsage: process.cpuUsage(),
      ...metadata,
    };

    this.metrics.conversions.push(conversionMetric);

    // Keep only last 1000 conversion metrics
    if (this.metrics.conversions.length > 1000) {
      this.metrics.conversions.shift();
    }

    // Check for performance degradation
    this.checkConversionPerformanceDegradation(conversionMetric);
  }

  /**
   * Track request performance
   * @param {string} method - HTTP method
   * @param {string} url - Request URL
   * @param {number} duration - Request duration in milliseconds
   * @param {number} statusCode - HTTP status code
   * @param {Object} metadata - Additional request metadata
   */
  static trackRequestPerformance(
    method,
    url,
    duration,
    statusCode,
    metadata = {}
  ) {
    const requestMetric = {
      method,
      url,
      duration,
      statusCode,
      timestamp: new Date(),
      success: statusCode < 400,
      ...metadata,
    };

    this.metrics.requests.push(requestMetric);

    // Keep only last 1000 request metrics
    if (this.metrics.requests.length > 1000) {
      this.metrics.requests.shift();
    }

    // Check response time alerts
    if (duration > this.THRESHOLDS.responseTimeMs) {
      this.generateAlert({
        type: "SLOW_RESPONSE",
        severity: "WARNING",
        message: `Slow response detected: ${duration}ms for ${method} ${url}`,
        metrics: requestMetric,
        recommendation:
          "Investigate performance bottlenecks in request processing",
      });
    }
  }

  /**
   * Check for conversion performance degradation
   * @param {Object} currentMetric - Current conversion metric
   */
  static checkConversionPerformanceDegradation(currentMetric) {
    const recentConversions = this.metrics.conversions
      .filter((c) => c.operation === currentMetric.operation && c.success)
      .slice(-20); // Last 20 successful conversions of same type

    if (recentConversions.length < 5) return; // Need minimum sample size

    const avgDuration = this.calculateAverage(
      recentConversions.map((c) => c.duration)
    );
    const stdDev = this.calculateStandardDeviation(
      recentConversions.map((c) => c.duration)
    );

    // Check if current conversion is significantly slower (3 standard deviations)
    if (currentMetric.duration > avgDuration + 3 * stdDev) {
      this.generateAlert({
        type: "CONVERSION_PERFORMANCE_DEGRADATION",
        severity: "WARNING",
        message: `Performance degradation in ${currentMetric.operation}: ${
          currentMetric.duration
        }ms (avg: ${avgDuration.toFixed(0)}ms)`,
        metrics: currentMetric,
        recommendation:
          "Monitor system resources and investigate conversion pipeline",
      });
    }
  }

  /**
   * Perform comprehensive performance analysis
   */
  static performPerformanceAnalysis() {
    const analysis = {
      timestamp: new Date().toISOString(),
      systemHealth: this.analyzeSystemHealth(),
      conversionPerformance: this.analyzeConversionPerformance(),
      requestPerformance: this.analyzeRequestPerformance(),
      recommendations: [],
    };

    // Generate performance recommendations
    analysis.recommendations =
      this.generatePerformanceRecommendations(analysis);

    // Log analysis results
    console.log(
      "Performance analysis completed:",
      JSON.stringify(analysis, null, 2)
    );

    return analysis;
  }

  /**
   * Analyze system health based on recent metrics
   * @returns {Object} System health analysis
   */
  static analyzeSystemHealth() {
    const recentMetrics = this.metrics.system.slice(-60); // Last 5 minutes of data

    if (recentMetrics.length === 0) {
      return {
        status: "UNKNOWN",
        score: 0,
        message: "Insufficient data for analysis",
      };
    }

    const avgMemoryUsage = this.calculateAverage(
      recentMetrics.map((m) => m.memoryUsagePercent)
    );
    const avgCpuUsage = this.calculateAverage(
      recentMetrics.map((m) => m.cpuUsagePercent)
    );
    const maxMemoryUsage = Math.max(
      ...recentMetrics.map((m) => m.memoryUsagePercent)
    );
    const maxCpuUsage = Math.max(
      ...recentMetrics.map((m) => m.cpuUsagePercent)
    );

    let score = 100;
    let status = "HEALTHY";
    let issues = [];

    // Memory analysis
    if (maxMemoryUsage > this.THRESHOLDS.memoryUsagePercent) {
      score -= 30;
      issues.push("High memory usage detected");
    }

    // CPU analysis
    if (maxCpuUsage > this.THRESHOLDS.cpuUsagePercent) {
      score -= 20;
      issues.push("High CPU usage detected");
    }

    // Trend analysis (memory growth)
    if (recentMetrics.length >= 10) {
      const firstHalf = recentMetrics.slice(
        0,
        Math.floor(recentMetrics.length / 2)
      );
      const secondHalf = recentMetrics.slice(
        Math.floor(recentMetrics.length / 2)
      );

      const firstHalfAvg = this.calculateAverage(
        firstHalf.map((m) => m.memoryUsageBytes)
      );
      const secondHalfAvg = this.calculateAverage(
        secondHalf.map((m) => m.memoryUsageBytes)
      );

      if (secondHalfAvg - firstHalfAvg > this.THRESHOLDS.memoryLeakThreshold) {
        score -= 25;
        status = "CRITICAL";
        issues.push("Potential memory leak detected");
      }
    }

    if (score < 50) {
      status = "CRITICAL";
    } else if (score < 75) {
      status = "WARNING";
    }

    return {
      status,
      score: Math.round(score),
      message:
        issues.length > 0 ? issues.join(", ") : "System performing normally",
      metrics: {
        avgMemoryUsage: Math.round(avgMemoryUsage),
        avgCpuUsage: Math.round(avgCpuUsage),
        maxMemoryUsage: Math.round(maxMemoryUsage),
        maxCpuUsage: Math.round(maxCpuUsage),
      },
    };
  }

  /**
   * Analyze conversion performance
   * @returns {Object} Conversion performance analysis
   */
  static analyzeConversionPerformance() {
    const recentConversions = this.metrics.conversions.slice(-100);

    if (recentConversions.length === 0) {
      return { status: "UNKNOWN", message: "No conversion data available" };
    }

    const successfulConversions = recentConversions.filter((c) => c.success);
    const failedConversions = recentConversions.filter((c) => !c.success);

    const successRate =
      (successfulConversions.length / recentConversions.length) * 100;
    const avgDuration =
      successfulConversions.length > 0
        ? this.calculateAverage(successfulConversions.map((c) => c.duration))
        : 0;

    let status = "GOOD";
    let issues = [];

    if (successRate < 95) {
      status = "WARNING";
      issues.push(`Low success rate: ${successRate.toFixed(1)}%`);
    }

    if (avgDuration > this.THRESHOLDS.conversionTimeoutMs * 0.8) {
      status = "WARNING";
      issues.push(`High average conversion time: ${avgDuration.toFixed(0)}ms`);
    }

    return {
      status,
      successRate: Math.round(successRate),
      avgDuration: Math.round(avgDuration),
      totalConversions: recentConversions.length,
      failedConversions: failedConversions.length,
      message:
        issues.length > 0
          ? issues.join(", ")
          : "Conversion performance within acceptable ranges",
    };
  }

  /**
   * Analyze request performance
   * @returns {Object} Request performance analysis
   */
  static analyzeRequestPerformance() {
    const recentRequests = this.metrics.requests.slice(-100);

    if (recentRequests.length === 0) {
      return { status: "UNKNOWN", message: "No request data available" };
    }

    const successfulRequests = recentRequests.filter((r) => r.success);
    const failedRequests = recentRequests.filter((r) => !r.success);

    const successRate =
      (successfulRequests.length / recentRequests.length) * 100;
    const avgResponseTime = this.calculateAverage(
      recentRequests.map((r) => r.duration)
    );
    const p95ResponseTime = this.calculatePercentile(
      recentRequests.map((r) => r.duration),
      95
    );

    let status = "GOOD";
    let issues = [];

    if (successRate < 99) {
      status = "WARNING";
      issues.push(`Request error rate: ${(100 - successRate).toFixed(1)}%`);
    }

    if (p95ResponseTime > this.THRESHOLDS.responseTimeMs) {
      status = "WARNING";
      issues.push(`P95 response time high: ${p95ResponseTime.toFixed(0)}ms`);
    }

    return {
      status,
      successRate: Math.round(successRate),
      avgResponseTime: Math.round(avgResponseTime),
      p95ResponseTime: Math.round(p95ResponseTime),
      totalRequests: recentRequests.length,
      failedRequests: failedRequests.length,
      message:
        issues.length > 0
          ? issues.join(", ")
          : "Request performance within acceptable ranges",
    };
  }

  /**
   * Generate performance recommendations based on analysis
   * @param {Object} analysis - Performance analysis results
   * @returns {Array} Performance recommendations
   */
  static generatePerformanceRecommendations(analysis) {
    const recommendations = [];

    // System health recommendations
    if (analysis.systemHealth.score < 75) {
      recommendations.push({
        category: "SYSTEM",
        priority: "HIGH",
        recommendation: "Optimize system resource usage",
        actions: [
          "Review memory allocation and garbage collection",
          "Monitor CPU-intensive operations",
          "Consider horizontal scaling if load is high",
        ],
      });
    }

    // Conversion performance recommendations
    if (analysis.conversionPerformance.successRate < 95) {
      recommendations.push({
        category: "CONVERSION",
        priority: "HIGH",
        recommendation: "Improve conversion reliability",
        actions: [
          "Review error handling in conversion pipeline",
          "Validate input document formats",
          "Check LibreOffice installation and configuration",
        ],
      });
    }

    if (analysis.conversionPerformance.avgDuration > 120000) {
      // 2 minutes
      recommendations.push({
        category: "CONVERSION",
        priority: "MEDIUM",
        recommendation: "Optimize conversion performance",
        actions: [
          "Enable preprocessing for document optimization",
          "Review conversion settings for quality vs speed trade-offs",
          "Consider caching frequently converted document types",
        ],
      });
    }

    // Request performance recommendations
    if (analysis.requestPerformance.p95ResponseTime > 10000) {
      recommendations.push({
        category: "REQUEST",
        priority: "MEDIUM",
        recommendation: "Improve response times",
        actions: [
          "Optimize database queries if applicable",
          "Review middleware performance",
          "Consider implementing response caching",
        ],
      });
    }

    return recommendations;
  }

  /**
   * Detect memory leaks using trend analysis
   */
  static detectMemoryLeaks() {
    const recentMetrics = this.metrics.system.slice(-60); // Last 5 minutes

    if (recentMetrics.length < 20) return; // Need sufficient data

    // Simple linear regression to detect memory growth trend
    const memoryTrend = this.calculateTrend(
      recentMetrics.map((m) => m.memoryUsageBytes)
    );

    if (memoryTrend.slope > 1000) {
      // Growing by more than 1KB per sample
      this.generateAlert({
        type: "MEMORY_LEAK_DETECTED",
        severity: "CRITICAL",
        message: `Memory leak detected: ${memoryTrend.slope.toFixed(
          0
        )} bytes per sample growth rate`,
        metrics: {
          slope: memoryTrend.slope,
          r2: memoryTrend.r2,
          sampleCount: recentMetrics.length,
        },
        recommendation:
          "Immediate investigation required - restart service if necessary",
      });
    }
  }

  /**
   * Generate performance alert
   * @param {Object} alert - Alert configuration
   */
  static generateAlert(alert) {
    const alertEntry = {
      ...alert,
      id: `ALERT-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
      timestamp: new Date().toISOString(),
    };

    this.metrics.alerts.push(alertEntry);

    // Keep only last 500 alerts
    if (this.metrics.alerts.length > 500) {
      this.metrics.alerts.shift();
    }

    // Log alert based on severity
    const logMessage = `PERFORMANCE ALERT: ${alert.message}`;
    if (alert.severity === "CRITICAL") {
      console.error(logMessage);
    } else {
      console.warn(logMessage);
    }

    // Here you would typically send alerts to monitoring systems
    // this.sendAlertToMonitoringSystem(alertEntry);
  }

  /**
   * Update performance baselines
   */
  static updateBaselines() {
    const recentMetrics = this.metrics.system.slice(-60); // Last 5 minutes

    if (recentMetrics.length >= 30) {
      // Need at least 30 samples
      this.baselines.memoryUsage = this.calculateAverage(
        recentMetrics.map((m) => m.memoryUsagePercent)
      );
      this.baselines.cpuUsage = this.calculateAverage(
        recentMetrics.map((m) => m.cpuUsagePercent)
      );
      this.baselines.lastUpdated = new Date().toISOString();

      console.log("Performance baselines updated");
    }
  }

  /**
   * Get comprehensive performance report
   * @returns {Object} Performance report
   */
  static getPerformanceReport() {
    return {
      timestamp: new Date().toISOString(),
      baselines: this.baselines,
      currentMetrics: this.collectSystemMetrics(),
      analysis: this.performPerformanceAnalysis(),
      alerts: this.metrics.alerts.slice(-10), // Last 10 alerts
      recommendations: this.generatePerformanceRecommendations(
        this.performPerformanceAnalysis()
      ),
    };
  }

  // Utility functions for statistical calculations

  /**
   * Calculate average of array of numbers
   * @param {Array<number>} values - Array of numbers
   * @returns {number} Average value
   */
  static calculateAverage(values) {
    return values.reduce((sum, value) => sum + value, 0) / values.length;
  }

  /**
   * Calculate standard deviation of array of numbers
   * @param {Array<number>} values - Array of numbers
   * @returns {number} Standard deviation
   */
  static calculateStandardDeviation(values) {
    const avg = this.calculateAverage(values);
    const squareDiffs = values.map((value) => Math.pow(value - avg, 2));
    return Math.sqrt(this.calculateAverage(squareDiffs));
  }

  /**
   * Calculate percentile from array of numbers
   * @param {Array<number>} values - Array of numbers
   * @param {number} percentile - Percentile to calculate (0-100)
   * @returns {number} Percentile value
   */
  static calculatePercentile(values, percentile) {
    const sorted = values.sort((a, b) => a - b);
    const index = (percentile / 100) * (sorted.length - 1);
    const lower = Math.floor(index);
    const upper = Math.ceil(index);
    const weight = index % 1;

    if (upper >= sorted.length) return sorted[sorted.length - 1];
    return sorted[lower] * (1 - weight) + sorted[upper] * weight;
  }

  /**
   * Calculate linear trend (simple linear regression)
   * @param {Array<number>} values - Array of numbers
   * @returns {Object} Trend analysis result
   */
  static calculateTrend(values) {
    const n = values.length;
    const x = Array.from({ length: n }, (_, i) => i);

    const sumX = x.reduce((sum, val) => sum + val, 0);
    const sumY = values.reduce((sum, val) => sum + val, 0);
    const sumXY = x.reduce((sum, val, i) => sum + val * values[i], 0);
    const sumXX = x.reduce((sum, val) => sum + val * val, 0);

    const slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
    const intercept = (sumY - slope * sumX) / n;

    // Calculate R-squared
    const yMean = sumY / n;
    const ssRes = values.reduce((sum, val, i) => {
      const predicted = slope * x[i] + intercept;
      return sum + Math.pow(val - predicted, 2);
    }, 0);
    const ssTot = values.reduce(
      (sum, val) => sum + Math.pow(val - yMean, 2),
      0
    );
    const r2 = 1 - ssRes / ssTot;

    return { slope, intercept, r2 };
  }

  /**
   * Shutdown performance monitoring
   */
  static shutdown() {
    console.log("Shutting down performance monitoring...");

    // Clear all monitoring intervals
    for (const [name, interval] of this.monitoringIntervals) {
      clearInterval(interval);
      console.log(`   Cleared monitoring interval: ${name}`);
    }

    this.monitoringIntervals.clear();

    // Generate final performance report
    const finalReport = this.getPerformanceReport();
    console.log("Final performance report generated");

    console.log("Performance monitoring shutdown complete");
  }
}

module.exports = PerformanceMonitor;
