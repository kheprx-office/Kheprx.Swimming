# Rename to Kheprx.BaseFrontend + Push to Remote — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the project identity to Kheprx.BaseFrontend (Tier 1 surfaces only) and publish the repo to the empty remote on a `main` branch.

**Architecture:** Two deliverables. Task 1 applies the five Tier-1 file renames, verifies the app still tests and builds under the new Angular project key, and commits. Task 2 wires the remote, renames `master` → `main`, and pushes with upstream tracking.

**Tech Stack:** Angular v20, Angular CLI, Jest, Git, GitHub.

## Global Constraints

- **Identifier (lowercase) form:** `kheprx.basefrontend` — for `package.json`/`package-lock.json` name fields and the `angular.json` project key. (npm forbids uppercase; npm and Angular both permit dots.)
- **Display form:** `Kheprx.BaseFrontend` — for the HTML `<title>` and README.
- **Tier 1 only.** Do NOT touch: the `'Hello, Base App Frontend'` sample string (`mock-api-data-source.ts`, `docs/DATA_SOURCE_SEAM.html`); `DB_NAME = 'base-frontend-crypto'` (`web-crypto-key-store.ts`, `docs/CORE_FILES.html`); the historical `docs/superpowers/specs/*` and `plans/*` markdown; the local folder name.
- **README:** rename only `Base App Frontend` occurrences. The phrase `Base Frontend` (the predecessor skeleton, e.g. README line 5) must remain unchanged.
- **Remote:** `https://github.com/kheprx-office/Kheprx.BaseFrontend.git` (exists, empty).

---

### Task 1: Apply Tier-1 renames and verify

**Files:**
- Modify: `package.json:2`
- Modify: `package-lock.json:2` and `package-lock.json:8`
- Modify: `angular.json:6`
- Modify: `src/index.html:5`
- Modify: `README.md:1` and `README.md:3`

**Interfaces:**
- Consumes: nothing.
- Produces: a committed working tree whose project identity is `kheprx.basefrontend` (identifier) / `Kheprx.BaseFrontend` (display), still Jest-green and build-clean. Task 2 consumes this commit.

- [ ] **Step 1: Edit `package.json`**

Change line 2 from:

```json
  "name": "base-app-frontend",
```

to:

```json
  "name": "kheprx.basefrontend",
```

- [ ] **Step 2: Edit `package-lock.json` (both occurrences)**

Replace both occurrences of `"name": "base-app-frontend"` (line 2, the root; and line 8, the `packages[""]` entry) with `"name": "kheprx.basefrontend"`. Keep surrounding indentation and the trailing comma exactly as-is.

- [ ] **Step 3: Edit `angular.json` project key**

Change line 6 from:

```json
    "base-frontend": {
```

to:

```json
    "kheprx.basefrontend": {
```

(The npm scripts call `ng build`/`ng serve` with no project name, so single-project resolution still works.)

- [ ] **Step 4: Edit `src/index.html` title**

Change line 5 from:

```html
  <title>BaseFrontend</title>
```

to:

```html
  <title>Kheprx.BaseFrontend</title>
```

- [ ] **Step 5: Edit `README.md` (heading + bold reference)**

Change line 1 from `# Base App Frontend` to `# Kheprx.BaseFrontend`.

Change line 3 from `**Base App Frontend** is an Angular v20 base architecture` to `**Kheprx.BaseFrontend** is an Angular v20 base architecture` (rest of the line unchanged).

Do NOT change `Base Frontend` on line 5 — that refers to the predecessor skeleton.

- [ ] **Step 6: Confirm no stray Tier-1 references remain**

Run: `git grep -n -i "base-app-frontend\|base app frontend"`
Expected: matches only in the out-of-scope files — `mock-api-data-source.ts`, `docs/DATA_SOURCE_SEAM.html`, and the historical `docs/superpowers/specs/*` + `plans/*` markdown. No matches in `package.json`, `package-lock.json`, `README.md`, `src/index.html`, or `angular.json`.

Run: `git grep -n '"base-frontend"' angular.json`
Expected: no output (the project key was renamed).

- [ ] **Step 7: Verify the test suite stays green**

Run: `npx jest`
Expected: PASS — all existing suites pass (the rename touches no test-referenced code).

- [ ] **Step 8: Verify the production build succeeds under the new project key**

Run: `npm run build`
Expected: build completes successfully with no project-resolution error (confirms the dotted `angular.json` key `kheprx.basefrontend` is valid).

- [ ] **Step 9: Commit**

```bash
git add package.json package-lock.json angular.json src/index.html README.md
git commit -m "chore: rename project to Kheprx.BaseFrontend"
```

---

### Task 2: Add remote, rename branch to main, and push

**Files:** none (git operations only).

**Interfaces:**
- Consumes: the `chore: rename project to Kheprx.BaseFrontend` commit from Task 1.
- Produces: the local repo pushed to the remote on `main` with upstream tracking set.

- [ ] **Step 1: Confirm a clean tree on `master`**

Run: `git status`
Expected: `On branch master`, `nothing to commit, working tree clean`.

- [ ] **Step 2: Add the remote**

Run: `git remote add origin https://github.com/kheprx-office/Kheprx.BaseFrontend.git`

Then confirm: `git remote -v`
Expected: `origin` lists the fetch and push URLs above.

- [ ] **Step 3: Rename `master` → `main`**

Run: `git branch -M main`

Then confirm: `git branch`
Expected: `* main`.

- [ ] **Step 4: Push with upstream tracking**

Run: `git push -u origin main`
Expected: objects upload and the output ends with `branch 'main' set up to track 'origin/main'.` (clean push — the remote is empty, so no reconcile/force needed).

- [ ] **Step 5: Verify the remote state**

Run: `git ls-remote --heads origin`
Expected: a single `refs/heads/main` entry whose SHA matches `git rev-parse HEAD`.

---

## Notes for the implementer

- If `git push` is rejected with "fetch first" / non-fast-forward, the remote was **not** empty after all. Stop and report — do not force-push. The design assumes an empty remote.
- If `npm run build` fails specifically on the project name, that contradicts the design assumption that Angular accepts dotted keys; stop and report rather than improvising a different name.
