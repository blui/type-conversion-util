# File Conversion API - Supported Conversions

## Document Conversions

### Legacy Microsoft Word (.DOC)

**Source:** DOC (Microsoft Word 97-2003)  
**Supported Targets:** 6 formats

| Target Format | Use Case                | Engine      | Status       |
| ------------- | ----------------------- | ----------- | ------------ |
| PDF           | Distribution, archival  | LibreOffice |  Production |
| DOCX          | Modernization           | LibreOffice |  Production |
| TXT           | Text extraction         | LibreOffice |  Production |
| RTF           | Cross-platform exchange | LibreOffice |  Production |
| ODT           | Open standard format    | LibreOffice |  Production |
| HTML/HTM      | Web publishing          | LibreOffice |  Production |

**Total DOC conversions:** 7 paths (HTML + HTM variant)

---

### Modern Microsoft Word (.DOCX)

**Source:** DOCX (Microsoft Word 2007+)  
**Supported Targets:** 3 formats

| Target Format | Use Case               | Engine      | Status       |
| ------------- | ---------------------- | ----------- | ------------ |
| PDF           | Distribution, archival | LibreOffice |  Production |
| TXT           | Text extraction        | LibreOffice |  Production |
| DOC           | Legacy compatibility   | LibreOffice |  Production |

**Total DOCX conversions:** 3 paths

---

### PDF Documents (.PDF)

**Source:** PDF (Portable Document Format)  
**Supported Targets:** 3 formats

| Target Format | Use Case          | Engine            | Status       |
| ------------- | ----------------- | ----------------- | ------------ |
| DOCX          | Editable document | LibreOffice       |  Production |
| DOC           | Legacy editable   | LibreOffice       |  Production |
| TXT           | Text extraction   | PdfService/iText7 |  Production |

**Total PDF conversions:** 3 paths

---

### Plain Text (.TXT)

**Source:** TXT (Plain Text)  
**Supported Targets:** 3 formats

| Target Format | Use Case           | Engine                 | Status       |
| ------------- | ------------------ | ---------------------- | ------------ |
| PDF           | Distribution       | PdfService/iText7      |  Production |
| DOCX          | Formatted document | DocumentFormat.OpenXml |  Production |
| DOC           | Legacy document    | DocumentFormat.OpenXml |  Production |

**Total TXT conversions:** 3 paths

---

### Microsoft Excel (.XLSX)

**Source:** XLSX (Microsoft Excel 2007+)  
**Supported Targets:** 2 formats

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| CSV           | Data export  | NPOI        |  Production |
| PDF           | Distribution | LibreOffice |  Production |

**Total XLSX conversions:** 2 paths

---

### CSV Files (.CSV)

**Source:** CSV (Comma Separated Values)  
**Supported Targets:** 1 format

| Target Format | Use Case           | Engine    | Status       |
| ------------- | ------------------ | --------- | ------------ |
| XLSX          | Spreadsheet format | CsvHelper |  Production |

**Total CSV conversions:** 1 path

---

### Microsoft PowerPoint (.PPTX)

**Source:** PPTX (Microsoft PowerPoint 2007+)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| PDF           | Distribution | LibreOffice |  Production |

**Total PPTX conversions:** 1 path

---

### Rich Text Format (.RTF)

**Source:** RTF (Rich Text Format)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| PDF           | Distribution | LibreOffice |  Production |

**Total RTF conversions:** 1 path

---

### XML Documents (.XML)

**Source:** XML (Extensible Markup Language)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine | Status       |
| ------------- | ------------ | ------ | ------------ |
| PDF           | Distribution | iText7 |  Production |

**Total XML conversions:** 1 path

---

### HTML Documents (.HTML/.HTM)

**Source:** HTML/HTM (Hypertext Markup Language)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine | Status              |
| ------------- | ------------ | ------ | ------------------- |
| PDF           | Distribution | iText7 |  Basic (text only) |

**Total HTML conversions:** 2 paths (HTML + HTM variant)

**Note:** Current HTML to PDF uses basic text rendering. Advanced HTML/CSS features not supported.

---

## LibreOffice Native Formats

### OpenDocument Text (.ODT)

**Source:** ODT (OpenDocument Text)  
**Supported Targets:** 2 formats

| Target Format | Use Case                | Engine      | Status       |
| ------------- | ----------------------- | ----------- | ------------ |
| PDF           | Distribution            | LibreOffice |  Production |
| DOCX          | Microsoft compatibility | LibreOffice |  Production |

**Total ODT conversions:** 2 paths

---

### OpenDocument Spreadsheet (.ODS)

**Source:** ODS (OpenDocument Spreadsheet)  
**Supported Targets:** 2 formats

| Target Format | Use Case                | Engine      | Status       |
| ------------- | ----------------------- | ----------- | ------------ |
| PDF           | Distribution            | LibreOffice |  Production |
| XLSX          | Microsoft compatibility | LibreOffice |  Production |

**Total ODS conversions:** 2 paths

---

### OpenDocument Presentation (.ODP)

**Source:** ODP (OpenDocument Presentation)  
**Supported Targets:** 2 formats

| Target Format | Use Case                | Engine      | Status       |
| ------------- | ----------------------- | ----------- | ------------ |
| PDF           | Distribution            | LibreOffice |  Production |
| PPTX          | Microsoft compatibility | LibreOffice |  Production |

**Total ODP conversions:** 2 paths

---

### OpenDocument Graphics (.ODG)

**Source:** ODG (OpenDocument Graphics)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| PDF           | Distribution | LibreOffice |  Production |

**Total ODG conversions:** 1 path

---

## OpenOffice Legacy Formats

### StarWriter (.SXW)

**Source:** SXW (StarWriter/OpenOffice 1.x Writer)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| PDF           | Distribution | LibreOffice |  Production |

**Total SXW conversions:** 1 path

---

### StarCalc (.SXC)

**Source:** SXC (StarCalc/OpenOffice 1.x Calc)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| PDF           | Distribution | LibreOffice |  Production |

**Total SXC conversions:** 1 path

---

### StarImpress (.SXI)

**Source:** SXI (StarImpress/OpenOffice 1.x Impress)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| PDF           | Distribution | LibreOffice |  Production |

**Total SXI conversions:** 1 path

---

### StarDraw (.SXD)

**Source:** SXD (StarDraw/OpenOffice 1.x Draw)  
**Supported Targets:** 1 format

| Target Format | Use Case     | Engine      | Status       |
| ------------- | ------------ | ----------- | ------------ |
| PDF           | Distribution | LibreOffice |  Production |

**Total SXD conversions:** 1 path

---

## Summary Statistics

### By Category

| Category                     | Input Formats | Output Formats | Total Paths |
| ---------------------------- | ------------- | -------------- | ----------- |
| **Document Conversions**     | 11            | 6              | 29          |
| **Spreadsheet Conversions**  | 3             | 3              | 5           |
| **Presentation Conversions** | 2             | 2              | 3           |
| **Total**                    | **16 unique** | **7 unique**   | **32**      |

### By Conversion Engine

| Engine                 | Conversions | Status             |
| ---------------------- | ----------- | ------------------ |
| LibreOffice            | 23 paths    |  Production Ready |
| iText7 (PdfService)    | 4 paths     |  Production Ready |
| DocumentFormat.OpenXml | 4 paths     |  Production Ready |
| NPOI                   | 1 path      |  Production Ready |
| CsvHelper              | 1 path      |  Production Ready |

### Most Common Conversions

| Conversion | Count    | Primary Use Case          |
| ---------- | -------- | ------------------------- |
| **→ PDF**  | 21 paths | Distribution, archival    |
| **→ TXT**  | 4 paths  | Text extraction, indexing |
| **→ DOCX** | 4 paths  | Modern document format    |
| **→ XLSX** | 2 paths  | Spreadsheet format        |
| **→ CSV**  | 1 path   | Data export               |

---

## Format Support Matrix

### Quick Reference Table

| From ↓ To →         | PDF | DOCX | DOC | TXT | XLSX | CSV | RTF | ODT | HTML | PPTX |
| ------------------- | --- | ---- | --- | --- | ---- | --- | --- | --- | ---- | ---- |
| **DOC**             |    |     | -   |    | -    | -   |    |    |     | -    |
| **DOCX**            |    | -    |    |    | -    | -   | -   | -   | -    | -    |
| **PDF**             | -   |     |    |    | -    | -   | -   | -   | -    | -    |
| **TXT**             |    |     |    | -   | -    | -   | -   | -   | -    | -    |
| **XLSX**            |    | -    | -   | -   | -    |    | -   | -   | -    | -    |
| **CSV**             | -   | -    | -   | -   |     | -   | -   | -   | -    | -    |
| **PPTX**            |    | -    | -   | -   | -    | -   | -   | -   | -    | -    |
| **ODT**             |    |     | -   | -   | -    | -   | -   | -   | -    | -    |
| **ODS**             |    | -    | -   | -   |     | -   | -   | -   | -    | -    |
| **ODP**             |    | -    | -   | -   | -    | -   | -   | -   | -    |     |
| **ODG**             |    | -    | -   | -   | -    | -   | -   | -   | -    | -    |
| **RTF**             |    | -    | -   | -   | -    | -   | -   | -   | -    | -    |
| **XML**             |    | -    | -   | -   | -    | -   | -   | -   | -    | -    |
| **HTML**            |    | -    | -   | -   | -    | -   | -   | -   | -    | -    |
| **SXW/SXC/SXI/SXD** |    | -    | -   | -   | -    | -   | -   | -   | -    | -    |

---

## API Endpoint

**POST** `/api/convert`

**Parameters:**

- `file`: Multipart file upload
- `targetFormat`: Target format extension (pdf, docx, txt, etc.)

**Example:**

```powershell
curl -X POST http://localhost:3000/api/convert `
  -F "file=@document.doc" `
  -F "targetFormat=pdf" `
  -o output.pdf
```

**Supported Formats Discovery:**

```powershell
curl http://localhost:3000/api/supported-formats
```

---

## Requirements

### LibreOffice Bundle Required For:

- All DOC/DOCX conversions
- All ODT/ODS/ODP/ODG conversions
- All SXW/SXC/SXI/SXD conversions
- XLSX → PDF
- PPTX → PDF
- RTF → PDF

**Without LibreOffice:** Only text/PDF operations and XLSX/CSV conversions work.

### .NET Libraries Used:

- **iText7**: PDF generation and manipulation
- **DocumentFormat.OpenXml**: Office document creation
- **NPOI**: Excel file processing
- **CsvHelper**: CSV parsing

---

## Limitations

### HTML to PDF

- Current implementation uses basic text rendering
- Advanced HTML/CSS features not supported
- Consider upgrading to proper HTML renderer for production

### Document Conversions

- Complex formatting may lose fidelity in some conversions
- Embedded objects depend on LibreOffice capabilities
- Macros are removed during conversion

---

## Performance Expectations

| Conversion Type | Small File | Medium File | Large File |
| --------------- | ---------- | ----------- | ---------- |
| DOC → PDF       | 2-4s       | 3-6s        | 6-12s      |
| XLSX → CSV      | <1s        | 1-2s        | 2-4s       |
| TXT → PDF       | <1s        | <1s         | 1-2s       |

**Note:** LibreOffice conversions include 2-4s startup overhead per conversion.

---
