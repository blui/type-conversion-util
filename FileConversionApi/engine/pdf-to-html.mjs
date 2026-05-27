import { readFile, writeFile } from "node:fs/promises";
import { getDocument, Util } from "pdfjs-dist/legacy/build/pdf.mjs";
import { createCanvas } from "@napi-rs/canvas";

const RENDER_SCALE = 2.0;
const MAX_PAGES = 2000;
const EXIT_OK = 0;
const EXIT_ERR = 1;

function fail(message) {
  process.stderr.write(`pdf-to-html: ${message}\n`);
  process.exit(EXIT_ERR);
}

function parseArgs(argv) {
  const positional = argv.slice(2);
  if (positional.length !== 2) {
    fail(
      "usage: node pdf-to-html.mjs <inputPdf> <outputHtml> " +
        "(exactly two arguments: input PDF path and output HTML path)"
    );
  }
  const [inputPdf, outputHtml] = positional;
  if (!inputPdf || !outputHtml) {
    fail("both <inputPdf> and <outputHtml> must be non-empty paths");
  }
  return { inputPdf, outputHtml };
}

function escapeHtml(text) {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

async function loadDocument(inputPdf) {
  let bytes;
  try {
    bytes = new Uint8Array(await readFile(inputPdf));
  } catch (cause) {
    fail(`cannot read input PDF '${inputPdf}': ${cause.message}`);
  }
  if (bytes.length === 0) {
    fail(`input PDF '${inputPdf}' is empty`);
  }
  try {
    return await getDocument({
      data: bytes,
      isEvalSupported: false,
      useSystemFonts: true,
    }).promise;
  } catch (cause) {
    fail(`pdf.js failed to parse '${inputPdf}': ${cause.message}`);
  }
}

function renderPageImage(page, viewport) {
  const width = Math.ceil(viewport.width);
  const height = Math.ceil(viewport.height);
  const canvas = createCanvas(width, height);
  const context = canvas.getContext("2d");
  return { canvas, context, width, height };
}

function buildTextSpans(textContent, viewport) {
  const spans = [];
  const items = textContent.items;
  for (let i = 0; i < items.length; i++) {
    const item = items[i];
    if (typeof item.str !== "string" || item.str.length === 0) {
      continue;
    }
    const matrix = Util.transform(viewport.transform, item.transform);
    const fontHeight = Math.hypot(matrix[2], matrix[3]);
    if (fontHeight <= 0) {
      continue;
    }
    const left = matrix[4];
    const top = matrix[5] - fontHeight;
    const angle = Math.atan2(matrix[1], matrix[0]);
    const rotation =
      Math.abs(angle) < 1e-6
        ? ""
        : ` transform:rotate(${angle}rad);transform-origin:0 100%;`;
    spans.push(
      `<span style="left:${left.toFixed(2)}px;top:${top.toFixed(2)}px;` +
        `font-size:${fontHeight.toFixed(2)}px;${rotation}">` +
        escapeHtml(item.str) +
        "</span>"
    );
  }
  return spans;
}

async function renderPage(pdf, pageNumber) {
  const page = await pdf.getPage(pageNumber);
  const viewport = page.getViewport({ scale: RENDER_SCALE });
  const { canvas, context, width, height } = renderPageImage(page, viewport);
  await page.render({ canvasContext: context, viewport, canvas }).promise;

  const png = canvas.toBuffer("image/png");
  if (!png || png.length === 0) {
    fail(`page ${pageNumber} rendered a zero-byte PNG (canvas backend missing?)`);
  }

  const textContent = await page.getTextContent();
  const spans = buildTextSpans(textContent, viewport);
  page.cleanup();

  return {
    width,
    height,
    dataUri: `data:image/png;base64,${png.toString("base64")}`,
    spans,
  };
}

function composePage(page, index) {
  return (
    `<div class="page" style="width:${page.width}px;height:${page.height}px;">` +
    `<img class="layer" src="${page.dataUri}" width="${page.width}" ` +
    `height="${page.height}" alt="page ${index + 1}">` +
    `<div class="text layer">${page.spans.join("")}</div>` +
    "</div>"
  );
}

function composeDocument(pages) {
  const style =
    "html,body{margin:0;padding:0;background:#525659;}" +
    ".page{position:relative;margin:8px auto;background:#fff;" +
    "box-shadow:0 0 6px rgba(0,0,0,0.5);overflow:hidden;}" +
    ".layer{position:absolute;top:0;left:0;}" +
    "img.layer{display:block;}" +
    ".text{color:transparent;}" +
    ".text span{position:absolute;white-space:pre;" +
    "line-height:1;cursor:text;color:transparent;}" +
    ".text span::selection{background:rgba(0,120,215,0.35);}";
  return (
    "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
    "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
    `<title>Rendered document (${pages.length} pages)</title>` +
    `<style>${style}</style></head><body>` +
    pages.map(composePage).join("") +
    "</body></html>"
  );
}

async function main() {
  const { inputPdf, outputHtml } = parseArgs(process.argv);
  const pdf = await loadDocument(inputPdf);

  const pageCount = pdf.numPages;
  if (pageCount <= 0) {
    fail(`'${inputPdf}' reports ${pageCount} pages`);
  }
  if (pageCount > MAX_PAGES) {
    fail(`'${inputPdf}' has ${pageCount} pages, exceeding the ${MAX_PAGES}-page bound`);
  }

  const pages = [];
  for (let pageNumber = 1; pageNumber <= pageCount; pageNumber++) {
    pages.push(await renderPage(pdf, pageNumber));
  }

  if (pages.length !== pageCount) {
    fail(`rendered ${pages.length} of ${pageCount} pages`);
  }

  const html = composeDocument(pages);
  try {
    await writeFile(outputHtml, html, "utf8");
  } catch (cause) {
    fail(`cannot write output HTML '${outputHtml}': ${cause.message}`);
  }

  await pdf.cleanup();
  await pdf.destroy();
  process.stdout.write(
    `pdf-to-html: ${pageCount} pages, ${html.length} bytes -> ${outputHtml}\n`
  );
  process.exit(EXIT_OK);
}

main().catch((cause) => fail(cause && cause.message ? cause.message : String(cause)));
