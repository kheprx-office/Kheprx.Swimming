// scripts/feature-doc/check-doc.mjs
import { readFileSync } from "node:fs";

const [, , file, ...rest] = process.argv;
if (!file) { console.error("usage: check-doc.mjs <html> [--require <substr> ...]"); process.exit(2); }
const requires = [];
for (let i = 0; i < rest.length; i++) if (rest[i] === "--require") requires.push(rest[++i]);

const html = readFileSync(file, "utf8");
const fails = [];

// Self-containment: no external script/link/font references.
if (/<script\b[^>]*\bsrc\s*=\s*["']https?:/i.test(html)) fails.push("external <script src=http...>");
if (/<link\b/i.test(html)) fails.push("<link> tag present");
if (/@font-face/i.test(html)) fails.push("@font-face present");
if (/url\(\s*["']?https?:/i.test(html)) fails.push("CSS url(http...) present");

for (const r of requires) if (!html.includes(r)) fails.push(`missing required substring: ${JSON.stringify(r)}`);

if (fails.length) { console.error(`check-doc FAIL (${file}):\n - ` + fails.join("\n - ")); process.exit(1); }
console.log(`check-doc OK (${file}): self-contained, ${requires.length} required substrings present`);
