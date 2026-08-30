import { test } from "node:test";
import assert from "node:assert/strict";
import { buildTree, statusMark } from "./build-tree.mjs";

test("statusMark maps known statuses and passes through unknowns", () => {
  assert.equal(statusMark("built"), "✓ built");
  assert.equal(statusMark("built-not-wired"), "⚠ built · not wired");
  assert.equal(statusMark("stub"), "⚠ stub");
  assert.equal(statusMark("planned"), "◌ planned");
  assert.equal(statusMark("weird"), "weird");
  assert.equal(statusMark(undefined), "");
});

test("buildTree merges shared folders and annotates leaves", () => {
  const tree = buildTree([
    { path: "core/auth/token-store.ts", role: "store", status: "built" },
    { path: "core/auth/usecases/login.use-case.ts", role: "use-case", status: "built" },
  ]);
  const lines = tree.split("\n");
  // "core" is the only top-level key -> printed once with the last-child connector
  assert.equal(lines.filter((l) => l.trim() === "└─ core").length, 1);
  assert.match(tree, /usecases/);
  assert.match(tree, /login\.use-case\.ts\s+use-case\s+✓ built/);
  assert.match(tree, /token-store\.ts\s+store\s+✓ built/);
});

test("buildTree appends a note after the status", () => {
  const tree = buildTree([
    { path: "core/auth/mock-auth.data-source.ts", role: "data-source", status: "built", note: "default" },
  ]);
  assert.match(tree, /mock-auth\.data-source\.ts\s+data-source\s+✓ built\s+·\s+default/);
});

test("buildTree handles empty input", () => {
  assert.equal(buildTree([]), "");
});
