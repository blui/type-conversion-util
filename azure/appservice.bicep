// Azure App Service (Windows) deployment for FileConversionApi.
//
// Provisions an App Service Plan (Windows) and a Web App configured to run the
// v1.0.0 forward conversion surface (DOC and DOCX to HTML and PDF) directly from
// a deploy.ps1-produced site bundle. The Web App's runtime stack is .NET 8 with
// in-process hosting via AspNetCoreModuleV2 (the same model the IIS deployment uses);
// the bundle's web.config, appsettings.json, LibreOffice/, libreoffice-profile-template/
// and engine/ subtrees deploy unchanged.
//
// LOAD-BEARING CONSTRAINTS. Do not change without replatforming the application.
//
//   Single instance only.
//     The application's in-process SemaphoreService caps concurrent conversions
//     via the appsettings.json Concurrency block. Scaling to additional workers
//     would split that semaphore across processes that do not coordinate,
//     producing orphaned soffice processes and corrupted per-operation profile
//     directories under App_Data\libreoffice-profiles\. The template pins
//     sku.capacity to 1 and siteConfig.numberOfWorkers to 1. No autoscale rules
//     are configured.
//
//   Always On.
//     The bundled LibreOffice runtime is ~500 MB on disk, and a per-process
//     profile is initialised from the committed template on the first
//     conversion a worker handles. The worker must stay warm to keep that
//     cold-start path inside the App Service 230 s request timeout.
//
//   ASPNETCORE_FORWARDEDHEADERS_ENABLED = true.
//     App Service terminates TLS at the front door; the worker receives plain
//     HTTP. This setting registers ForwardedHeaders middleware early in the
//     request pipeline, which consumes X-Forwarded-Proto so UseHttpsRedirection()
//     recognises the original https scheme. Without it, every HTTP request emits
//     a 307 to https:// that the front door normalises back to HTTP at the
//     worker, producing a redirect loop. No source change is required. The IIS
//     deployment does not set this variable and is unaffected.
//
//   WEBSITE_LOAD_USER_PROFILE = 1.
//     Loads the worker process user profile so LibreOffice's Win32 dependencies
//     (font fallback, COM activation, GDI+ shaping) resolve through the profile
//     they expect rather than the minimal sandbox profile.
//
//   API key as a @secure() parameter.
//     The initial API key is passed at deploy time and bound to the
//     Security__ApiKeys__0 app setting (double-underscore is the .NET
//     configuration provider's nested-array convention). Operators rotate by
//     editing app settings post-deploy or by switching to a Key Vault reference;
//     see DEPLOYMENT-AZURE.md "API key rotation".
//
// PREREQUISITES NOT CREATED BY THIS TEMPLATE.
//   - A resource group (operator runs az group create first).
//   - Optional Application Insights / Log Analytics workspace; wire its
//     connection string via the applicationInsightsConnectionString parameter
//     to enable auto-instrumentation.
//   - Optional custom domain + certificate; bind via az webapp config hostname
//     after the Web App exists.
//   - Optional Private Endpoint / VNET integration; configure on the Web App
//     after the Web App exists (requires a P-tier SKU).

@description('Azure region for all resources. Defaults to the resource group region.')
param location string = resourceGroup().location

@description('App Service Plan name. Convention: asp-fileconversion-<env>.')
param appServicePlanName string

@description('Web App name. Must be globally unique within azurewebsites.net.')
param webAppName string

@description('App Service Plan SKU. P1V3 recommended for production-grade LibreOffice headroom; B2 acceptable for low-volume internal use.')
@allowed([
  'B2'
  'B3'
  'S1'
  'S2'
  'P1V3'
  'P2V3'
])
param skuName string = 'P1V3'

@description('Initial API key bound to Security__ApiKeys__0. Supply via --parameters at deploy time; never commit.')
@secure()
param apiKey string

@description('Optional Application Insights connection string. Leave empty to skip auto-instrumentation.')
@secure()
param applicationInsightsConnectionString string = ''

@description('Tags applied to every resource. Defaults identify the workload and provisioning tool; extend at deploy time with environment or cost-centre tags as required.')
param tags object = {
  workload: 'fileconversionapi'
  managedBy: 'bicep'
}

var skuTier = {
  B2: 'Basic'
  B3: 'Basic'
  S1: 'Standard'
  S2: 'Standard'
  P1V3: 'PremiumV3'
  P2V3: 'PremiumV3'
}

var baseAppSettings = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
    value: 'true'
  }
  {
    name: 'WEBSITE_LOAD_USER_PROFILE'
    value: '1'
  }
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
    value: '0'
  }
  {
    name: 'Security__RequireApiKey'
    value: 'true'
  }
  {
    name: 'Security__ApiKeys__0'
    value: apiKey
  }
]

var appInsightsSettings = empty(applicationInsightsConnectionString) ? [] : [
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: applicationInsightsConnectionString
  }
  {
    name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
    value: '~3'
  }
]

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier[skuName]
    capacity: 1
  }
  kind: 'app'
  properties: {
    reserved: false
    perSiteScaling: false
    zoneRedundant: false
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  tags: tags
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    clientAffinityEnabled: false
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: true
      http20Enabled: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: false
      webSocketsEnabled: false
      healthCheckPath: '/health'
      loadBalancing: 'LeastRequests'
      numberOfWorkers: 1
      remoteDebuggingEnabled: false
      requestTracingEnabled: false
      detailedErrorLoggingEnabled: false
      appSettings: concat(baseAppSettings, appInsightsSettings)
    }
  }
}

resource webAppLogs 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: webApp
  name: 'logs'
  properties: {
    applicationLogs: {
      fileSystem: {
        level: 'Warning'
      }
    }
    httpLogs: {
      fileSystem: {
        retentionInMb: 35
        retentionInDays: 7
        enabled: true
      }
    }
    detailedErrorMessages: {
      enabled: false
    }
    failedRequestsTracing: {
      enabled: false
    }
  }
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output webAppName string = webApp.name
output appServicePlanId string = appServicePlan.id
output managedIdentityPrincipalId string = webApp.identity.principalId
