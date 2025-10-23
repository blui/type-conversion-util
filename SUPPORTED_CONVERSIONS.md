# Supported Conversions

Complete list of supported document format conversions.

## Summary

| Category | Input Formats | Output Formats | Total Paths |
|----------|---------------|----------------|-------------|
| Documents | 11 | 6 | 29 |
| Spreadsheets | 3 | 3 | 5 |
| Presentations | 2 | 2 | 3 |
| **Total** | **16 unique** | **7 unique** | **32** |

## Conversion Engines

| Engine | Conversions | Status |
|--------|-------------|--------|
| LibreOffice | 23 paths | Production |
| iText7 (PdfService) | 4 paths | Production |
| DocumentFormat.OpenXml | 4 paths | Production |
| NPOI | 1 path | Production |
| CsvHelper | 1 path | Production |

## Most Common Conversions

| Target | Paths | Primary Use |
|--------|-------|-------------|
| PDF | 21 | Distribution, archival |
| TXT | 4 | Text extraction, indexing |
| DOCX | 4 | Modern document format |
| XLSX | 2 | Spreadsheet format |
| CSV | 1 | Data export |

## Format Support Matrix

| From ↓ To → | PDF | DOCX | DOC | TXT | XLSX | CSV | RTF | ODT | HTML | PPTX |
|-------------|-----|------|-----|-----|------|-----|-----|-----|------|------|
| DOC | ✓ | ✓ | - | ✓ | - | - | ✓ | ✓ | ✓ | - |
| DOCX | ✓ | - | ✓ | ✓ | - | - | - | - | - | - |
| PDF | - | ✓ | ✓ | ✓ | - | - | - | - | - | - |
| TXT | ✓ | ✓ | ✓ | - | - | - | - | - | - | - |
| XLSX | ✓ | - | - | - | - | ✓ | - | - | - | - |
| CSV | - | - | - | - | ✓ | - | - | - | - | - |
| PPTX | ✓ | - | - | - | - | - | - | - | - | - |
| ODT | ✓ | ✓ | - | - | - | - | - | - | - | - |
| ODS | ✓ | - | - | - | ✓ | - | - | - | - | - |
| ODP | ✓ | - | - | - | - | - | - | - | - | ✓ |
| ODG | ✓ | - | - | - | - | - | - | - | - | - |
| RTF | ✓ | - | - | - | - | - | - | - | - | - |
| XML | ✓ | - | - | - | - | - | - | - | - | - |
| HTML | ✓ | - | - | - | - | - | - | - | - | - |
| SXW/SXC/SXI/SXD | ✓ | - | - | - | - | - | - | - | - | - |

## Document Conversions

**Legacy Microsoft Word (.DOC) → 6 formats**
| Target | Use Case | Engine |
|--------|----------|--------|
| PDF | Distribution, archival | LibreOffice |
| DOCX | Modernization | LibreOffice |
| TXT | Text extraction | LibreOffice |
| RTF | Cross-platform | LibreOffice |
| ODT | Open standard | LibreOffice |
| HTML/HTM | Web publishing | LibreOffice |

**Modern Microsoft Word (.DOCX) → 3 formats**
| Target | Use Case | Engine |
|--------|----------|--------|
| PDF | Distribution | LibreOffice |
| TXT | Text extraction | LibreOffice |
| DOC | Legacy compatibility | LibreOffice |

**PDF → 3 formats**
| Target | Use Case | Engine |
|--------|----------|--------|
| DOCX | Editable document | LibreOffice |
| DOC | Legacy editable | LibreOffice |
| TXT | Text extraction | PdfService/iText7 |

**Plain Text (.TXT) → 3 formats**
| Target | Use Case | Engine |
|--------|----------|--------|
| PDF | Distribution | PdfService/iText7 |
| DOCX | Formatted document | DocumentFormat.OpenXml |
| DOC | Legacy document | DocumentFormat.OpenXml |

**Other Formats → PDF**
| Format | Engine | Status |
|--------|--------|--------|
| RTF | LibreOffice | Production |
| XML | iText7 | Production |
| HTML/HTM | iText7 | Basic (text only) |

## Spreadsheet Conversions

**Excel (.XLSX) → 2 formats**
| Target | Use Case | Engine |
|--------|----------|--------|
| CSV | Data export | NPOI |
| PDF | Distribution | LibreOffice |

**CSV → 1 format**
| Target | Use Case | Engine |
|--------|----------|--------|
| XLSX | Spreadsheet format | CsvHelper |

## Presentation Conversions

**PowerPoint (.PPTX) → PDF**
| Engine | Status |
|--------|--------|
| LibreOffice | Production |

## OpenDocument Formats

**ODT (Text) → 2 formats**
- PDF (LibreOffice)
- DOCX (LibreOffice)

**ODS (Spreadsheet) → 2 formats**
- PDF (LibreOffice)
- XLSX (LibreOffice)

**ODP (Presentation) → 2 formats**
- PDF (LibreOffice)
- PPTX (LibreOffice)

**ODG (Graphics) → PDF**
- PDF (LibreOffice)

## OpenOffice Legacy Formats

All convert to PDF via LibreOffice:
- SXW (StarWriter)
- SXC (StarCalc)
- SXI (StarImpress)
- SXD (StarDraw)

## API Usage

**POST** `/api/convert`

```powershell
curl -X POST http://localhost:3000/api/convert `
  -F "file=@document.doc" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

**Discover supported formats:**
```powershell
curl http://localhost:3000/api/supported-formats
```

**API documentation:**
```
http://localhost:3000/api-docs
```

## Requirements

**LibreOffice Required For:**
- All DOC/DOCX conversions
- All ODT/ODS/ODP/ODG conversions
- All SXW/SXC/SXI/SXD conversions
- XLSX → PDF, PPTX → PDF, RTF → PDF

**Without LibreOffice:**
- Text/PDF operations work
- XLSX/CSV conversions work
- Other conversions will fail

**.NET Libraries:**
- iText7: PDF generation and manipulation
- DocumentFormat.OpenXml: Office document creation
- NPOI: Excel file processing
- CsvHelper: CSV parsing

## Limitations

**HTML to PDF:**
- Basic text rendering only
- Advanced HTML/CSS not supported
- Consider proper HTML renderer for production

**Document Conversions:**
- Complex formatting may lose fidelity
- Embedded objects depend on LibreOffice capabilities
- Macros are removed during conversion

## Performance

| Conversion Type | Small (1-5 pg) | Medium (10-20 pg) | Large (50+ pg) |
|-----------------|----------------|-------------------|----------------|
| DOC → PDF | 2-4s | 3-6s | 6-12s |
| XLSX → CSV | <1s | 1-2s | 2-4s |
| TXT → PDF | <1s | <1s | 1-2s |

Note: LibreOffice conversions include 2-4s startup overhead per conversion.

---
