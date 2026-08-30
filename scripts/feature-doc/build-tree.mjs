// scripts/feature-doc/build-tree.mjs
// Renders a flat list of flow files into an annotated ASCII tree.
// Mirrored inline in docs/frontend/auth-feature-flows.html (keep in sync).

export function statusMark(s) {
  return ({
    "built": "✓ built",
    "built-not-wired": "⚠ built · not wired",
    "stub": "⚠ stub",
    "planned": "◌ planned",
  })[s] || (s || "");
}

export function buildTree(files) {
  const root = { children: {} };
  for (const f of files || []) {
    const parts = String(f.path).split("/").filter(Boolean);
    let node = root;
    parts.forEach((p, i) => {
      node.children = node.children || {};
      node.children[p] = node.children[p] || { name: p, children: {} };
      node = node.children[p];
      if (i === parts.length - 1) node.meta = f;
    });
  }
  const lines = [];
  const walk = (node, prefix) => {
    const keys = Object.keys(node.children || {});
    keys.forEach((k, i) => {
      const child = node.children[k];
      const last = i === keys.length - 1;
      let line = prefix + (last ? "└─ " : "├─ ") + k;
      if (child.meta) {
        const ann = [child.meta.role, statusMark(child.meta.status)].filter(Boolean).join("  ");
        if (ann) line = (line.length >= 44 ? line + "  " : line.padEnd(46)) + ann;
        if (child.meta.note) line += "  · " + child.meta.note;
      }
      lines.push(line.replace(/\s+$/, ""));
      if (child.children && Object.keys(child.children).length) walk(child, prefix + (last ? "   " : "│  "));
    });
  };
  walk(root, "");
  return lines.join("\n");
}
