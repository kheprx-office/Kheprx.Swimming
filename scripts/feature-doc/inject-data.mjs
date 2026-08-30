// scripts/feature-doc/inject-data.mjs
import { readFileSync, writeFileSync } from "node:fs";

function arg(name) { const i = process.argv.indexOf(name); return i >= 0 ? process.argv[i + 1] : null; }
const file = arg("--file"), matchId = arg("--match-id"), matchText = arg("--match-text"), data = arg("--data");
if (!file || !data || (!matchId && !matchText)) {
  console.error("usage: --file <html> (--match-id <id> | --match-text <substr>) --data <jsfile>");
  process.exit(2);
}
const html = readFileSync(file, "utf8");
const payload = readFileSync(data, "utf8");

// Find all <script ...>...</script> spans.
const re = /<script\b[^>]*>[\s\S]*?<\/script>/gi;
let target = null, m;
while ((m = re.exec(html)) !== null) {
  const block = m[0];
  const openEnd = block.indexOf(">") + 1;
  const inner = block.slice(openEnd, block.lastIndexOf("</script>"));
  const openTag = block.slice(0, openEnd);
  if (matchId && new RegExp(`id\\s*=\\s*["']${matchId}["']`).test(openTag)) { target = { m, openEnd }; break; }
  if (matchText && inner.includes(matchText)) { target = { m, openEnd }; break; }
}
if (!target) { console.error("inject-data: no matching <script> block found"); process.exit(1); }
const block = target.m[0];
const openTag = block.slice(0, target.openEnd);
const rebuilt = `${openTag}\n${payload.trim()}\n</script>`;
const out = html.slice(0, target.m.index) + rebuilt + html.slice(target.m.index + block.length);
writeFileSync(file, out);
console.log(`inject-data: replaced ${matchId ? "#" + matchId : `block containing "${matchText}"`} in ${file} (${payload.length} bytes)`);
