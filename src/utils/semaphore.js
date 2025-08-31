/**
 * Semaphore Utility for Concurrency Control
 *
 * Implements a simple semaphore with bounded queue to control concurrent operations.
 * Provides rate limiting functionality with 429 responses when queue is saturated.
 * Used to prevent resource exhaustion during file conversion operations.
 */

class Semaphore {
  /**
   * Initialize semaphore with concurrency and queue limits
   *
   * @param {number} maxConcurrent - Maximum number of concurrent operations
   * @param {number} maxQueue - Maximum number of queued operations
   */
  constructor(maxConcurrent, maxQueue) {
    this.maxConcurrent = Math.max(1, maxConcurrent);
    this.maxQueue = Math.max(0, maxQueue);
    this.current = 0;
    this.queue = [];
  }

  /**
   * Try to acquire a semaphore slot without waiting
   * Returns true if slot is available, false otherwise
   *
   * @returns {boolean} True if slot acquired, false if not available
   */
  tryAcquire() {
    if (this.current < this.maxConcurrent) {
      this.current += 1;
      return true;
    }
    return false;
  }

  /**
   * Acquire a semaphore slot, waiting if necessary
   * Returns a promise that resolves with a release function
   * Rejects with "queue_saturated" error if queue is full
   *
   * @returns {Promise<Function>} Promise resolving to release function
   */
  acquire() {
    return new Promise((resolve, reject) => {
      // Try to acquire immediately if slot available
      if (this.tryAcquire()) {
        return resolve(this.release.bind(this));
      }

      // Check if queue has capacity
      if (this.queue.length >= this.maxQueue) {
        return reject(new Error("queue_saturated"));
      }

      // Add to queue and wait
      this.queue.push(resolve);
    });
  }

  /**
   * Release a semaphore slot
   * Processes next queued operation if any exist
   * Decrements current count if no operations are queued
   */
  release() {
    if (this.queue.length > 0) {
      // Process next queued operation
      const resolve = this.queue.shift();
      resolve(this.release.bind(this));
    } else {
      // Decrement current count when no operations are queued
      this.current = Math.max(0, this.current - 1);
    }
  }
}

module.exports = Semaphore;
