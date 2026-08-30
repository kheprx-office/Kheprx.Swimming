# Rename to Kheprx.BaseFrontend + Push to Remote — Design Spec

**Date:** 2026-06-29
**Project:** `C:\Users\envnt\Desktop\Base App Frontend`
**Remote:** `https://github.com/kheprx-office/Kheprx.BaseFrontend.git`

## Goal

Rename the project's identity to **Kheprx.BaseFrontend** and publish it to the
empty GitHub repository at the remote above, on a `main` branch.

## Naming decisions

The literal string `Kheprx.BaseFrontend` is a valid GitHub repo name but **not** a
valid npm `package.json` name (npm forbids uppercase). So the name maps to two forms:

- **Package/identifier form (lowercase):** `kheprx.basefrontend` — used wherever a
  machine-readable package or project key is required. npm and Angular both permit
  dots in names, so this mirrors the repo name as closely as a valid identifier allows.
- **Display form:** `Kheprx.BaseFrontend` — used in human-facing surfaces (HTML
  title, README).

## Scope — Tier 1 only (approved)

Rename the project identity in these files:

| File | Current | New |
|---|---|---|
| `package.json` | `"name": "base-app-frontend"` | `"name": "kheprx.basefrontend"` |
| `package-lock.json` (2 occurrences) | `"base-app-frontend"` | `"kheprx.basefrontend"` |
| `angular.json` | project key `base-frontend` | `kheprx.basefrontend` |
| `src/index.html` | `<title>BaseFrontend</title>` | `<title>Kheprx.BaseFrontend</title>` |
| `README.md` (heading + 1 reference) | `Base App Frontend` | `Kheprx.BaseFrontend` |

### Explicitly out of scope

- **Tier 2 (cosmetic sample content)** — the `'Hello, Base App Frontend'` string in
  `mock-api-data-source.ts` and its echo in `docs/DATA_SOURCE_SEAM.html`. Left unchanged.
- **Tier 3 (runtime identifiers + history)** — `DB_NAME = 'base-frontend-crypto'` in
  `web-crypto-key-store.ts` (a runtime IndexedDB identifier, not a display name) and its
  reference in `docs/CORE_FILES.html`; and the dated historical spec/plan markdown under
  `docs/superpowers/`. Left unchanged.
- **Local folder name** — `C:\Users\envnt\Desktop\Base App Frontend` stays as-is
  (renaming the active working directory mid-session is risky and unnecessary; the
  GitHub repo name is independent of the local folder).

## Verification

The `angular.json` project key is changed from `base-frontend` to `kheprx.basefrontend`.
Angular's project-name validation regex permits dots, and the npm scripts (`ng build`,
`ng serve`) do not reference the project by name (single-project resolution), so the
rename is safe. Confirm by running both:

- `npx jest` — test suite stays green.
- `npm run build` — production build succeeds with the new project key.

## Push procedure

1. Apply the Tier 1 renames.
2. Commit: `chore: rename project to Kheprx.BaseFrontend`.
3. `git remote add origin https://github.com/kheprx-office/Kheprx.BaseFrontend.git`
4. `git branch -M main` — rename local `master` to `main`.
5. `git push -u origin main` — clean push (the remote repo is empty).

## Success criteria

- Tier 1 files reference the new name; Tier 2/3 untouched.
- `npx jest` and `npm run build` both pass after the rename.
- The repo is pushed to the remote on a `main` branch with upstream tracking set.
- The GitHub repository shows the project on `main`.
