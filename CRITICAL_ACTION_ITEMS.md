# Critical Action Items

## Immediate Actions Required Before Deployment

### 1. Bundle LibreOffice Runtime

**CRITICAL - MUST DO BEFORE DEPLOYMENT**

The LibreOffice bundle is NOT present in the repository. Office document conversions (DOCX, XLSX, PPTX to PDF) will fail without it.

```powershell
# Run this command from project root:
.\bundle-libreoffice.ps1 -UltraMinimal

# This will:
# 1. Copy LibreOffice runtime from system installation
# 2. Create a minimal ~200MB bundle (vs 600MB full)
# 3. Place files in FileConversionApi/LibreOffice/
```

**Impact if skipped:**

- DOCX to PDF: Will fail
- XLSX to PDF: Will fail
- PPTX to PDF: Will fail
- PDF to DOCX: Will fail
- CSV, image, and text conversions: Will still work

### 2. Test Deployment Locally

Before deploying to production, test the complete flow:

```powershell
# 1. Build project
dotnet build FileConversionApi/FileConversionApi.csproj --configuration Release

# 2. Run locally
dotnet run --project FileConversionApi/FileConversionApi.csproj --urls=http://localhost:3000

# 3. Test health
curl http://localhost:3000/health

# 4. Test API docs
Start-Process "http://localhost:3000/api-docs"

# 5. Test Office conversion (requires LibreOffice bundle)
curl -X POST http://localhost:3000/api/convert `
  -F "file=@test.docx" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

### 3. Review Production Configuration

Before deploying, review and customize `appsettings.json`:

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": [
      "192.168.1.0/24", // UPDATE with your network
      "10.0.0.0/8"
    ]
  },
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 30 // UPDATE based on expected load
      }
    ]
  }
}
```

## Deployment Checklist

### Pre-Deployment

- [ ] LibreOffice bundle created (`.\bundle-libreoffice.ps1 -UltraMinimal`)
- [ ] Local testing completed successfully
- [ ] appsettings.json reviewed and customized
- [ ] .NET 8 Runtime installed on target server
- [ ] IIS installed and configured (Windows) or systemd ready (Linux)
- [ ] Firewall rules configured
- [ ] Backup of current deployment (if upgrading)

### Deployment

- [ ] Run deployment script: `.\deploy.ps1` (Windows IIS)
- [ ] Verify health endpoint: `curl http://server/health`
- [ ] Verify Swagger UI: `http://server/api-docs`
- [ ] Test sample conversion
- [ ] Check logs for errors
- [ ] Verify permissions on App_Data directory

### Post-Deployment

- [ ] Monitor health endpoint for 24 hours
- [ ] Review logs for errors
- [ ] Test all supported conversion types
- [ ] Verify rate limiting is working
- [ ] Confirm IP whitelist is effective (if enabled)
- [ ] Document any issues or adjustments made

## Known Issues and Limitations

### LibreOffice Bundle Not in Repository

**Issue**: The full LibreOffice runtime (~200-600MB) is not committed to the repository.

**Reason**: Large binary files are typically excluded from version control.

**Solution**: Run `.\bundle-libreoffice.ps1` before each deployment.

**Automation**: Consider adding LibreOffice bundle to CI/CD pipeline:

```yaml
# Example CI/CD step
- name: Bundle LibreOffice
  run: |
    choco install libreoffice-fresh
    .\bundle-libreoffice.ps1 -UltraMinimal
```

### Swagger Enabled in Production

**Status**: Swagger is now enabled for all environments (including production).

**Security Consideration**: Swagger exposes API documentation publicly.

**Options**:

1. Keep enabled (recommended for internal networks)
2. Disable in production: Set `Development:EnableSwagger=false` in appsettings.json
3. Add authentication to Swagger UI

**Current Implementation**: Enabled with no authentication (suitable for internal intranet deployments).

### IP Filtering Default

**Current State**: IP filtering is configured but may be disabled by default depending on environment.

**Recommendation**: Enable for production:

```json
{
  "Security": {
    "EnableIPFiltering": true,
    "IPWhitelist": ["your-network-cidr"]
  }
}
```

## Performance Tuning

### Initial Settings

The default configuration is conservative:

```json
{
  "Concurrency": {
    "MaxConcurrentConversions": 2,
    "MaxQueueSize": 10
  }
}
```

### Tuning Recommendations

Based on your server specifications:

**Low-end server (2 cores, 4GB RAM)**:

- MaxConcurrentConversions: 1-2
- MaxQueueSize: 5-10

**Mid-range server (4 cores, 8GB RAM)**:

- MaxConcurrentConversions: 2-4
- MaxQueueSize: 10-20

**High-end server (8+ cores, 16GB+ RAM)**:

- MaxConcurrentConversions: 4-8
- MaxQueueSize: 20-40

**Monitor and adjust** based on actual performance metrics available at `/health/detailed`.

## Security Considerations

### Intranet-Only Deployment

This API is designed for internal network deployment only.

**Do NOT expose to public internet without**:

1. Adding authentication (JWT, OAuth, etc.)
2. Implementing additional input validation
3. Adding virus scanning for uploaded files
4. Implementing file content sanitization
5. Adding DDoS protection
6. Regular security audits

### Current Security Features

- IP whitelisting (configure in appsettings.json)
- Rate limiting (30 requests/minute default)
- File size limits (50MB default)
- File type validation
- Path traversal prevention
- MIME type validation
- Security headers (CSP, X-Frame-Options, etc.)

## Monitoring

### Health Endpoints

Monitor these endpoints for system health:

```powershell
# Basic health (for load balancers)
curl http://server/health

# Detailed health (for monitoring dashboards)
curl http://server/health/detailed
```

### Logs

Monitor application logs:

**Windows**:

```powershell
# File logs
Get-Content C:\inetpub\logs\file-conversion-api-*.log -Tail 50 -Wait

# Event logs
Get-EventLog -LogName Application -Source FileConversionApi -Newest 20
```

**Linux**:

```bash
tail -f /var/log/file-conversion-api/file-conversion-api.log
```

### Alerts

Set up alerts for:

- Health endpoint returns non-200 status
- Error rate > 5%
- CPU usage > 80% sustained
- Memory usage > 90%
- Queue depth > 50% of max
- Disk space < 10% free

## Support and Documentation

### Documentation Files

- `README.md` - Overview and quick start
- `ARCHITECTURE.md` - System architecture and design
- `DEPLOYMENT_NOTES.md` - Comprehensive deployment guide
- `FileConversionApi/IIS_DEPLOYMENT_README.md` - IIS-specific instructions
- `CODE_REVIEW_SUMMARY.md` - Code review findings
- `CRITICAL_ACTION_ITEMS.md` - This file

### API Documentation

- **Interactive docs**: `http://server/api-docs` (Swagger UI)
- **OpenAPI spec**: `http://server/swagger/v1/swagger.json`

### Getting Help

For issues:

1. Check health endpoint: `/health/detailed`
2. Review logs (see Monitoring section above)
3. Consult DEPLOYMENT_NOTES.md for troubleshooting
4. Check CODE_REVIEW_SUMMARY.md for known issues

## Final Pre-Deployment Command Sequence

```powershell
# 1. Navigate to project root
cd C:\path\to\type-conversion-util

# 2. Bundle LibreOffice
.\bundle-libreoffice.ps1 -UltraMinimal

# 3. Build project
dotnet build FileConversionApi/FileConversionApi.csproj --configuration Release

# 4. Test locally
dotnet run --project FileConversionApi/FileConversionApi.csproj --urls=http://localhost:3000

# In another terminal, test:
curl http://localhost:3000/health
curl http://localhost:3000/api-docs

# 5. Deploy to IIS (as Administrator)
cd FileConversionApi
.\deploy.ps1

# 6. Verify deployment
curl http://localhost/health
curl http://localhost/api-docs

# 7. Test conversion
curl -X POST http://localhost/api/convert `
  -F "file=@sample.docx" `
  -F "targetFormat=pdf" `
  -o test.pdf
```

If all tests pass, the deployment is successful.
