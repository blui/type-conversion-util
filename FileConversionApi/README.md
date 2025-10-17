# LibreOffice SDK Integration

This directory contains the LibreOffice SDK binaries that are bundled with the application for self-contained deployment.

The SDK binaries are required for document conversion functionality and are included as part of the build output.

## Contents
- soffice.exe - Main LibreOffice executable
- uno.dll - UNO API libraries
- Other required LibreOffice runtime files

## Deployment
These binaries are copied to the output directory during build and are included in the deployment package.