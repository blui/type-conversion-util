# Deployment Guide: Azure App Service (Windows)

How to deploy the File Conversion API to Azure App Service Windows as an alternate
deployment target to the on-premises IIS path documented in `DEPLOYMENT.md`.

This stream is fully additive: it shares the same `FileConversionApi/deploy.ps1`
output bundle the IIS deploy uses, adds a zip wrapper, and provisions an App
Service Plan + Web App via Bicep. No source code, web.config, appsettings.json,
or `deploy.ps1` changes are required.

## Prerequisites

| Component         | Details                                                                                       |
| ----------------- | --------------------------------------------------------------------------------------------- |
| OS (build host)   | Windows 11 or Windows Server 2016+; same prerequisites as `DEPLOYMENT.md` for bundle building |
| Azure CLI         | 2.55.0+ ([install](https://learn.microsoft.com/cli/azure/install-azure-cli))                  |
| Bicep CLI         | 0.24.0+ (bundled with Azure CLI; verify with `az bicep version`)                              |
| Azure subscription| Contributor on the target resource group                                                      |
| App Service Plan  | Windows P1V3 recommended (B2 minimum for LibreOffice headroom)                                |
| Resource Group    | Pre-created by operator; this template does not provision RGs                                 |

### Verify the toolchain

```powershell
az --version
az bicep version
az account show
```

If `az account show` returns nothing, run `az login` first.

## Building the Deployment Package

Do this on the **development machine** (assumes LibreOffice is installed and the
bundle / profile-template have been built once via `bundle-libreoffice.ps1` and
`create-libreoffice-profile-template.ps1` from the repo root; see `DEPLOYMENT.md`
Step 1 and Step 2 for the procedure).

```powershell
# From the repo root:
.\deploy-azure.ps1

# Produces: deploy\release.zip (~280-310 MB compressed; ~850 MB uncompressed)
```

`deploy-azure.ps1` is a thin wrapper around `FileConversionApi\deploy.ps1`. It
runs the existing IIS-shaped bundle build (publish + LibreOffice + profile
template + Node engine + App_Data directories) and then compresses the
`deploy\release\` tree into a single zip ready for `az webapp deploy`.

**What gets packaged** (identical to the IIS bundle):

- .NET 8 application (`FileConversionApi.dll` + dependencies, ~50 MB)
- LibreOffice bundle with VC++ runtime DLLs (~500 MB)
- Pre-initialized LibreOffice profile template (~2 KB)
- Bundled Node engine (~180 MB total, restored by `npm ci --omit=dev` at package time):
  - `engine\node\node.exe` runtime
  - `engine\node_modules\` (pdfjs-dist, @napi-rs/canvas)
  - `engine\pdf-to-html.mjs` (PDF -> HTML, hop 2 of DOC/DOCX -> HTML)
- `web.config`, `appsettings.json`
- Empty `App_Data\` subdirectories (logs, temp/uploads, temp/converted, libreoffice-profiles)

## One-Time Provisioning

Provision the App Service Plan + Web App via Bicep. This is done once per
environment; subsequent code deploys reuse the provisioned resources.

### 1. Prepare the parameters file

Copy `azure\parameters.example.json` to a working file outside the repo (the
working file will contain the API key and must NOT be committed):

```powershell
Copy-Item azure\parameters.example.json $env:USERPROFILE\fileconversion-prod.parameters.json
```

Edit the working file:

```json
{
  "parameters": {
    "appServicePlanName": { "value": "asp-fileconversion-prod" },
    "webAppName":         { "value": "app-fileconversion-prod-<your-suffix>" },
    "skuName":            { "value": "P1V3" },
    "apiKey":             { "value": "apikey_live_<32+ chars from a secure RNG>" },
    "applicationInsightsConnectionString": { "value": "" }
  }
}
```

| Parameter | Notes |
| --- | --- |
| `webAppName` | Must be globally unique within `azurewebsites.net`. Use a project + environment + random suffix shape, e.g. `app-fileconversion-prod-a3k9` |
| `skuName`    | `B2` works for low-volume internal use; `P1V3` recommended for production-grade headroom. `B1` is **not** recommended (LibreOffice peak memory exceeds 1.75 GB on larger fixtures) |
| `apiKey`     | Bound to `Security__ApiKeys__0` app setting. Generate with `[Convert]::ToBase64String([byte[]](1..32 \| ForEach-Object { Get-Random -Maximum 256 }))` or your team's secret-rotation tool |
| `applicationInsightsConnectionString` | Optional. Leave empty to skip auto-instrumentation; supply a connection string to enable it |

### 2. Lint and validate the template

```powershell
cd $env:USERPROFILE  # validate from outside the repo so the param file is not exposed

az bicep build --file <repo-root>\azure\appservice.bicep

az deployment group validate `
  --resource-group <rg> `
  --template-file <repo-root>\azure\appservice.bicep `
  --parameters @fileconversion-prod.parameters.json
```

Both must succeed before the create step.

### 3. Create the resources

```powershell
az deployment group create `
  --resource-group <rg> `
  --template-file <repo-root>\azure\appservice.bicep `
  --parameters @fileconversion-prod.parameters.json `
  --name "fileconversion-$(Get-Date -Format yyyyMMdd-HHmmss)"
```

This creates two resources: the App Service Plan and the Web App. The Web App
boots with the `appSettings` configured but no application code yet, so
`/health` returns 503 until the first deploy completes.

The deploy output emits four values (`webAppUrl`, `webAppName`,
`appServicePlanId`, `managedIdentityPrincipalId`). Save the `webAppUrl` for
verification and the `managedIdentityPrincipalId` if you plan to grant the Web
App access to other Azure resources (Key Vault, Storage, ACR).

## Each Deploy

Once provisioned, each subsequent code deploy is two commands:

```powershell
# 1. Build the package
.\deploy-azure.ps1

# 2. Push to Azure
az webapp deploy `
  --resource-group <rg> `
  --name <webAppName> `
  --src-path .\deploy\release.zip `
  --type zip
```

`az webapp deploy --type zip` uploads the package to the Kudu engine, which
unpacks it into `D:\home\site\wwwroot\` and restarts the worker. Expect 5-10
minutes for the upload + restart cycle on a P1V3.

### Track the deploy

```powershell
# Tail the deployment-trigger log in another terminal:
az webapp log tail --resource-group <rg> --name <webAppName>
```

## Configuration

App Service configuration is read from **App Settings** (env vars), which
override `appsettings.json` via the standard ASP.NET Core configuration provider
chain. The Bicep template pre-populates the load-bearing settings; this section
covers the ones operators tune post-deploy.

### App settings provisioned by the Bicep template

| Setting | Value | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Loads `appsettings.Production.json` if present; matches the v1.0.0 IIS default |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | Auto-registers ForwardedHeaders middleware at the start of the request pipeline so `UseHttpsRedirection()` sees `https` from the front door's `X-Forwarded-Proto` and does not 307-loop |
| `WEBSITE_LOAD_USER_PROFILE` | `1` | Loads the worker user profile; LibreOffice's Win32 font/COM dependencies expect it |
| `WEBSITE_RUN_FROM_PACKAGE` | `0` | Keeps the file system writable so LibreOffice can create per-operation profile directories under `App_Data\libreoffice-profiles\` |
| `Security__RequireApiKey` | `true` | Mandatory API-key authentication |
| `Security__ApiKeys__0` | (from `apiKey` parameter) | Initial API key. Rotate via the Configuration blade or the rotation procedure below |

### API key rotation

App Service does not hot-reload env-var-bound app settings on edit; the worker
restarts when the Configuration blade is saved.

```powershell
# Single-key rotation (replaces the existing key; any caller still on the old
# key gets a 401 once the restart completes):
az webapp config appsettings set `
  --resource-group <rg> `
  --name <webAppName> `
  --settings "Security__ApiKeys__0=apikey_live_<new-secret>"

# Dual-key rotation (callers transition at their own pace; remove old key once
# the metrics confirm zero callers on it):
az webapp config appsettings set `
  --resource-group <rg> `
  --name <webAppName> `
  --settings "Security__ApiKeys__0=apikey_live_<new>" "Security__ApiKeys__1=apikey_live_<old>"
```

For Key Vault-backed rotation, set the app setting value to a Key Vault
reference of the form `@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/<name>/)`
and grant the Web App's system-assigned managed identity the `Key Vault Secrets
User` role on the vault. The managed identity principal ID is one of the
`outputs` of the Bicep deployment.

### Rate limits

`appsettings.json` configures `AspNetCoreRateLimit` with per-IP defaults that
the Bicep template does not override:

| Endpoint              | Limit       |
| --------------------- | ----------- |
| `*` (any endpoint)    | 30 / minute |
| `POST /api/convert`   | 10 / minute |
| `GET /health`         | 60 / minute |

Heavier smoke runs from a single caller IP will trip 429s. Disable rate
limiting post-deploy for the duration of the run:

```powershell
az webapp config appsettings set `
  --resource-group <rg> `
  --name <webAppName> `
  --settings "IpRateLimiting__EnableEndpointRateLimiting=false"
```

Re-enable when smoke is done:

```powershell
az webapp config appsettings delete `
  --resource-group <rg> `
  --name <webAppName> `
  --setting-names IpRateLimiting__EnableEndpointRateLimiting
```

### Scaling guard rails

**Do not enable autoscale rules and do not raise the instance count.** The
application's in-process `SemaphoreService` caps concurrent conversions via the
`appsettings.json` `Concurrency.MaxConcurrentConversions` setting (default 2).
Adding workers would split that semaphore across processes that do not
coordinate, producing orphaned `soffice.exe` processes and corrupted
per-operation profile directories under `App_Data\libreoffice-profiles\`.

If throughput becomes a constraint, the architectural fix is to externalize the
semaphore (e.g., Redis-backed) and rebuild the LibreOffice profile model to
support cross-instance coordination. That is a milestone-scoped change, not an
ops-blade tweak.

### File handling overrides

The defaults (relative `App_Data\` paths) work on App Service unchanged. If
operations prefer absolute paths or a per-environment isolation pattern, set:

```powershell
az webapp config appsettings set `
  --resource-group <rg> `
  --name <webAppName> `
  --settings `
    "FileHandling__TempDirectory=D:\home\site\wwwroot\App_Data\temp\uploads" `
    "FileHandling__OutputDirectory=D:\home\site\wwwroot\App_Data\temp\converted"
```

LibreOffice profile directories remain under `App_Data\libreoffice-profiles\`
(hardcoded in `LibreOfficeProcessManager.cs`, matching the IIS deploy).

### Logging

The Bicep template enables file system logging at level Warning (the default
for App Service). To raise the level temporarily for debugging:

```powershell
az webapp log config `
  --resource-group <rg> `
  --name <webAppName> `
  --application-logging filesystem `
  --level information
```

Application logs live under `D:\home\LogFiles\Application\` and are accessible
via Kudu (`https://<webAppName>.scm.azurewebsites.net/api/logs/recent`) or:

```powershell
az webapp log tail --resource-group <rg> --name <webAppName>
```

For long-term retention, link an Application Insights workspace by editing the
Bicep deploy's `applicationInsightsConnectionString` parameter and re-running
the deployment, or set the app setting post-deploy.

## Verification

### Health check

```powershell
$siteUrl = "https://<webAppName>.azurewebsites.net"

curl "$siteUrl/health"
# Expect: HTTP 200 with JSON body whose Status equals "Healthy"

# If 503, inspect logs (the rest of this section assumes 200):
az webapp log tail --resource-group <rg> --name <webAppName>
```

### Conversion smoke

```powershell
# Use the API key bound at deploy time.
$apiKey = "apikey_live_<your-key>"

# Forward direction: DOCX -> HTML
curl.exe -fsS `
  -H "X-API-Key: $apiKey" `
  -F "file=@samples\template.docx" `
  -F "targetFormat=html" `
  "$siteUrl/api/convert" `
  -o azure-smoke.html

# Forward direction: DOCX -> PDF
curl.exe -fsS `
  -H "X-API-Key: $apiKey" `
  -F "file=@samples\template.docx" `
  -F "targetFormat=pdf" `
  "$siteUrl/api/convert" `
  -o azure-smoke.pdf

# Reverse direction: HTML -> PDF (pipeline-output HTML rebuilt from page rasters via iText7)
curl.exe -fsS `
  -H "X-API-Key: $apiKey" `
  -F "file=@samples\template.html" `
  -F "targetFormat=pdf" `
  "$siteUrl/api/convert" `
  -o azure-smoke-html.pdf

# Reverse direction: HTML -> DOCX (pipeline-output HTML rebuilt from page rasters via OpenXml)
curl.exe -fsS `
  -H "X-API-Key: $apiKey" `
  -F "file=@samples\template.html" `
  -F "targetFormat=docx" `
  "$siteUrl/api/convert" `
  -o azure-smoke-html.docx

# Inspect the forward direction:
Test-Path azure-smoke.html  # True
Test-Path azure-smoke.pdf   # True
(Get-Content azure-smoke.html -Raw) -match '<html'   # True
(Get-Content azure-smoke.html -Raw) -match 'src="data:'   # True (embedded images)
(Get-Content azure-smoke.html -Raw) -match 'src="file://'  # False (no local-path leaks)

# Inspect the reverse direction (magic-byte spot-checks; full fidelity is operator-visual):
Test-Path azure-smoke-html.pdf   # True
Test-Path azure-smoke-html.docx  # True
[BitConverter]::ToString((Get-Content azure-smoke-html.pdf  -AsByteStream -TotalCount 5))  # 25-50-44-46-2D (%PDF-)
[BitConverter]::ToString((Get-Content azure-smoke-html.docx -AsByteStream -TotalCount 4))  # 50-4B-03-04 (ZIP/OOXML)
```

If any conversion fails, jump to **Troubleshooting**.

### Concurrent no-orphans check (optional but recommended once per deploy)

```powershell
Start-Job { curl.exe -fsS -H "X-API-Key: $using:apiKey" -F "file=@samples\template.docx" -F "targetFormat=html" "$using:siteUrl/api/convert" -o c1.html } | Out-Null
Start-Job { curl.exe -fsS -H "X-API-Key: $using:apiKey" -F "file=@samples\template.docx" -F "targetFormat=html" "$using:siteUrl/api/convert" -o c2.html } | Out-Null
Get-Job | Wait-Job | Receive-Job

Test-Path c1.html  # True
Test-Path c2.html  # True
```

Then, in the Kudu console (`https://<webAppName>.scm.azurewebsites.net`),
navigate to **Debug Console -> CMD** and run:

```
dir D:\home\site\wwwroot\App_Data\libreoffice-profiles
dir D:\home\site\wwwroot\App_Data\temp\uploads
dir D:\home\site\wwwroot\App_Data\temp\converted
```

All three should be empty (or contain only the placeholder directories from the
deploy bundle) once both responses complete. Any leftover `op-<guid>` directory
indicates a profile-cleanup regression worth investigating.

## Troubleshooting

### 500.30 on every request

The .NET runtime failed to load. Most common cause is a corrupted upload.

```powershell
# Re-run the deploy and watch the upload + restart cycle:
az webapp log tail --resource-group <rg> --name <webAppName>
.\deploy-azure.ps1
az webapp deploy --resource-group <rg> --name <webAppName> --src-path .\deploy\release.zip --type zip
```

If the error persists, inspect `D:\home\LogFiles\eventlog.xml` via Kudu for the
specific module load failure.

### 307 redirect loop on every URL

`ASPNETCORE_FORWARDEDHEADERS_ENABLED` is not set or was inadvertently cleared.
Restore it:

```powershell
az webapp config appsettings set `
  --resource-group <rg> `
  --name <webAppName> `
  --settings "ASPNETCORE_FORWARDEDHEADERS_ENABLED=true"
```

### Conversions fail with "LibreOffice executable not found"

The bundle did not unpack correctly, or the deploy ran against an older bundle.

Via Kudu **Debug Console -> CMD**:

```
dir D:\home\site\wwwroot\LibreOffice\program\soffice.exe
```

If the file is missing, redeploy. If it is present but the error persists,
check the application logs for the actual exception detail:

```powershell
az webapp log tail --resource-group <rg> --name <webAppName> | Select-String "LibreOffice|soffice"
```

### Conversions fail with exit code -1073741515 (DLL_NOT_FOUND)

The bundled Visual C++ runtime DLLs are missing from the `LibreOffice\program\`
directory. This is a bundle-build issue, not an App Service issue.

```powershell
# On the build host:
.\bundle-libreoffice.ps1 -Force
.\deploy-azure.ps1
az webapp deploy --resource-group <rg> --name <webAppName> --src-path .\deploy\release.zip --type zip
```

### Conversions hang / time out at 230 seconds

App Service's per-request idle timeout is 230 seconds. A hung LibreOffice
process or a slow conversion past that ceiling is the most common cause.

Check for orphans in Kudu:

```
tasklist /FI "IMAGENAME eq soffice.exe"
```

If orphans are present, restart the worker:

```powershell
az webapp restart --resource-group <rg> --name <webAppName>
```

If hangs recur, raise the LibreOffice timeout below the App Service ceiling
(default is 300 s; the App Service ceiling is hard at 230 s, so lower the app
to 220 s to surface a clean app-level timeout before the platform kills the
request):

```powershell
az webapp config appsettings set `
  --resource-group <rg> `
  --name <webAppName> `
  --settings "LibreOffice__TimeoutSeconds=220"
```

### Cold start is slow on the first request after deploy

Expected. Always On keeps the worker warm; the first conversion still
initialises a per-process LibreOffice profile from the template (10-30 s on
P1V3). Subsequent conversions reuse the warm process and complete in seconds.

If the first conversion times out at 230 s, increase the App Service Plan SKU
(P1V3 is the minimum that produces consistent first-request times under 30 s
on our fixture set).

### Where to look for additional diagnostics

| Symptom               | First place to look                                                        |
| --------------------- | -------------------------------------------------------------------------- |
| Module-load failure   | `https://<webAppName>.scm.azurewebsites.net/api/logs/recent` (Kudu)        |
| HTTP errors           | `az webapp log tail`                                                       |
| Native crash          | `D:\home\LogFiles\CrashDumps\` via Kudu                                    |
| Slow conversions      | Application Insights (if wired) request + dependency telemetry             |
| Worker process state  | Kudu **Process Explorer**                                                  |
| Configuration drift   | `az webapp config appsettings list --resource-group <rg> --name <webAppName>` |

## Cross-reference

The on-premises IIS deployment procedure is in `DEPLOYMENT.md`. Both streams
produce their site bundle via `FileConversionApi\deploy.ps1`; only the
downstream packaging and provisioning differ.
