# LibreOffice SDK Integration

This directory contains the LibreOffice SDK binaries that are bundled with the application for self-contained deployment.

## Required Files

To enable document conversion functionality, you need to copy the following LibreOffice binaries to this directory:

### Core Executable
- program/soffice.exe - Main LibreOffice executable
- program/soffice.bin - LibreOffice binary

### UNO Libraries
- program/uno.dll
- program/uno.ini
- program/cppu3.dll
- program/cppuhelper3MSC.dll
- program/sal3.dll
- program/salhelper3MSC.dll

### Runtime Dependencies
- All files from LibreOffice program/ directory
- Required shared libraries and dependencies

## Setup Instructions

1. Download LibreOffice SDK or extract from a LibreOffice installation
2. Copy the program/ directory contents to this LibreOffice/ directory
3. Ensure soffice.exe is executable
4. Build the application - binaries will be copied to output directory

## Deployment

These binaries are automatically included in the build output and deployment package, ensuring the application is self-contained and doesn't require LibreOffice to be installed on the target server.

## Development Notes

During development, the application will look for LibreOffice in:
1. Bundled binaries in output directory
2. This source directory (for development)
3. Configured paths (for backwards compatibility)
4. System installations (fallback, not recommended for production)