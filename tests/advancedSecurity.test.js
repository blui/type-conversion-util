/**
 * Unit Tests for Advanced Security Middleware - IP Whitelist
 *
 * Critical security component requiring comprehensive test coverage.
 * Tests IPv4, IPv6, CIDR matching, and edge cases per PR review requirements.
 */

describe("AdvancedSecurity - IP Whitelist", () => {
  let AdvancedSecurity;
  let req, res, next;

  beforeEach(() => {
    // Clear module cache to get fresh instance
    jest.resetModules();
    delete process.env.IP_WHITELIST;

    req = {
      ip: null,
      connection: { remoteAddress: null },
      id: "test-request-id",
    };
    res = {
      status: jest.fn().mockReturnThis(),
      json: jest.fn().mockReturnThis(),
    };
    next = jest.fn();
  });

  afterEach(() => {
    delete process.env.IP_WHITELIST;
    jest.resetModules();
  });

  describe("IPv4 Exact Matching", () => {
    test("should allow exact IPv4 match", () => {
      process.env.IP_WHITELIST = "192.168.1.100";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "192.168.1.100";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
      expect(res.status).not.toHaveBeenCalled();
    });

    test("should block non-matching IPv4", () => {
      process.env.IP_WHITELIST = "192.168.1.100";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "192.168.1.101";
      middleware(req, res, next);

      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
      expect(res.json).toHaveBeenCalledWith(
        expect.objectContaining({
          error: "Access Denied",
          requestId: "test-request-id",
        })
      );
    });

    test("should handle multiple whitelisted IPs", () => {
      process.env.IP_WHITELIST = "10.0.0.1,192.168.1.100,172.16.0.5";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "172.16.0.5";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
      expect(res.status).not.toHaveBeenCalled();
    });
  });

  describe("IPv4 CIDR Matching", () => {
    test("should allow IPv4 in CIDR range /24", () => {
      process.env.IP_WHITELIST = "192.168.1.0/24";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "192.168.1.50";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
      expect(res.status).not.toHaveBeenCalled();
    });

    test("should block IPv4 outside CIDR range /24", () => {
      process.env.IP_WHITELIST = "192.168.1.0/24";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "192.168.2.50";
      middleware(req, res, next);

      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should handle /32 CIDR (exact IP)", () => {
      process.env.IP_WHITELIST = "10.0.0.5/32";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "10.0.0.5";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // Test blocking - recreate mocks properly
      next = jest.fn();
      res = {
        status: jest.fn().mockReturnThis(),
        json: jest.fn().mockReturnThis(),
      };
      req.ip = "10.0.0.6";
      middleware(req, res, next);
      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should handle /16 CIDR (large range)", () => {
      process.env.IP_WHITELIST = "172.16.0.0/16";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "172.16.255.255";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });
  });

  describe("IPv6 Exact Matching", () => {
    test("should allow exact IPv6 match", () => {
      process.env.IP_WHITELIST = "2001:db8::1";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "2001:db8::1";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
      expect(res.status).not.toHaveBeenCalled();
    });

    test("should block non-matching IPv6", () => {
      process.env.IP_WHITELIST = "2001:db8::1";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "2001:db8::2";
      middleware(req, res, next);

      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should match IPv6 localhost (::1)", () => {
      process.env.IP_WHITELIST = "::1";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "::1";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });

    test("should match expanded IPv6 localhost (0:0:0:0:0:0:0:1)", () => {
      process.env.IP_WHITELIST = "::1";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "0:0:0:0:0:0:0:1";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });
  });

  describe("IPv6 CIDR Matching", () => {
    test("should allow IPv6 in CIDR range", () => {
      process.env.IP_WHITELIST = "2001:db8::/32";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "2001:db8:0:0:0:0:0:1";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });

    test("should block IPv6 outside CIDR range", () => {
      process.env.IP_WHITELIST = "2001:db8::/32";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "2001:db9::1";
      middleware(req, res, next);

      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });
  });

  describe("IPv4-mapped IPv6 Addresses (PR Issue Fix)", () => {
    test("should match IPv4-mapped IPv6 (::ffff:192.168.1.100) against IPv4 whitelist", () => {
      process.env.IP_WHITELIST = "192.168.1.100";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "::ffff:192.168.1.100";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
      expect(res.status).not.toHaveBeenCalled();
    });

    test("should match IPv4-mapped IPv6 against IPv4 CIDR", () => {
      process.env.IP_WHITELIST = "192.168.1.0/24";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "::ffff:192.168.1.50";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });

    test("should match ::1 against ::1/128 (IPv6 localhost)", () => {
      // Note: ::1 (IPv6) and 127.0.0.0/8 (IPv4) are different address spaces
      // For dual-stack localhost support, whitelist both explicitly
      process.env.IP_WHITELIST = "::1/128";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "::1";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });

    test("should match ::ffff:127.0.0.1 against 127.0.0.1", () => {
      process.env.IP_WHITELIST = "127.0.0.1";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "::ffff:127.0.0.1";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });
  });

  describe("Mixed IPv4/IPv6 Whitelist", () => {
    test("should handle mixed IPv4 and IPv6 in whitelist", () => {
      process.env.IP_WHITELIST = "192.168.1.100,2001:db8::1,10.0.0.0/8";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      // Test IPv4
      req.ip = "192.168.1.100";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // Test IPv6
      jest.resetAllMocks();
      req.ip = "2001:db8::1";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // Test IPv4 CIDR
      jest.resetAllMocks();
      req.ip = "10.5.5.5";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();
    });
  });

  describe("Edge Cases and Error Handling", () => {
    test("should reject invalid IP addresses", () => {
      process.env.IP_WHITELIST = "192.168.1.100";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "not-an-ip";
      middleware(req, res, next);

      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should handle missing req.ip gracefully", () => {
      process.env.IP_WHITELIST = "192.168.1.100";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = null;
      req.connection.remoteAddress = null;
      middleware(req, res, next);

      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should handle malformed CIDR notation gracefully", () => {
      process.env.IP_WHITELIST = "192.168.1.0/invalid";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "192.168.1.50";
      middleware(req, res, next);

      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should skip whitelist check when disabled", () => {
      // No IP_WHITELIST env var
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "1.2.3.4";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
      expect(res.status).not.toHaveBeenCalled();
    });

    test("should handle empty IP_WHITELIST string", () => {
      process.env.IP_WHITELIST = "";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "1.2.3.4";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });

    test("should handle whitespace in IP_WHITELIST", () => {
      process.env.IP_WHITELIST = "  192.168.1.100  ,  10.0.0.0/8  ";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      req.ip = "192.168.1.100";
      middleware(req, res, next);

      expect(next).toHaveBeenCalled();
    });
  });

  describe("Real-World Scenarios", () => {
    test("should handle typical corporate network setup", () => {
      // Corporate network: 10.0.0.0/8 and specific VPN endpoint
      process.env.IP_WHITELIST = "10.0.0.0/8,203.0.113.50";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      // Internal corporate IP
      req.ip = "10.245.67.123";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // VPN endpoint
      next = jest.fn();
      res = {
        status: jest.fn().mockReturnThis(),
        json: jest.fn().mockReturnThis(),
      };
      req.ip = "203.0.113.50";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // Blocked external IP
      next = jest.fn();
      res = {
        status: jest.fn().mockReturnThis(),
        json: jest.fn().mockReturnThis(),
      };
      req.ip = "8.8.8.8";
      middleware(req, res, next);
      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should handle localhost in various forms", () => {
      process.env.IP_WHITELIST = "127.0.0.1,::1";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      // IPv4 localhost
      req.ip = "127.0.0.1";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // IPv6 localhost
      jest.resetAllMocks();
      req.ip = "::1";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // IPv4-mapped IPv6 localhost
      jest.resetAllMocks();
      req.ip = "::ffff:127.0.0.1";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();
    });

    test("should handle dual-stack deployment", () => {
      // Allow both IPv4 and IPv6 ranges for same network
      process.env.IP_WHITELIST = "192.168.1.0/24,2001:db8::/32";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      // IPv4 client
      req.ip = "192.168.1.55";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();

      // IPv6 client
      jest.resetAllMocks();
      req.ip = "2001:db8::abcd:1234";
      middleware(req, res, next);
      expect(next).toHaveBeenCalled();
    });
  });

  describe("PR Issue Regression Tests", () => {
    test("should not produce NaN on IPv6 addresses", () => {
      process.env.IP_WHITELIST = "192.168.1.0/24";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      // This would have caused NaN with the old _ipToInt implementation
      req.ip = "2001:db8::1";
      middleware(req, res, next);

      // Should reject cleanly, not crash
      expect(next).not.toHaveBeenCalled();
      expect(res.status).toHaveBeenCalledWith(403);
    });

    test("should not cause false 403s for legitimate IPv6 users", () => {
      process.env.IP_WHITELIST = "::1,2001:db8::/32";
      AdvancedSecurity = require("../src/middleware/advancedSecurity");
      const middleware = AdvancedSecurity.ipWhitelistMiddleware();

      // All these should be allowed
      const validIPs = [
        "::1",
        "0:0:0:0:0:0:0:1",
        "2001:db8::1",
        "2001:db8:0:0:0:0:0:5",
        "2001:db8::abcd:ef01",
      ];

      validIPs.forEach((ip) => {
        jest.resetAllMocks();
        req.ip = ip;
        middleware(req, res, next);
        expect(next).toHaveBeenCalled();
        expect(res.status).not.toHaveBeenCalled();
      });
    });
  });
});
