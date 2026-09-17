# Contributing to Aegis-ID

## Setup

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download) (pinned
version in `global.json`) and Node 18.19+ (for the Angular dashboard).

```bash
git clone https://github.com/SaniAdamou14/Aegis-ID.git
cd Aegis-ID
dotnet build AegisId.sln
dotnet test
```

For the dashboard (`Aegis.Api` + Angular, see the root README's
[Dashboard](README.md#dashboard) section):

```bash
npm install
npm run dev   # starts Aegis.Api and the Angular dev server together
```

## Workflow

`master` is protected by a branch ruleset requiring the `build-and-test`
and `gitleaks` CI checks to pass — changes land through a pull request:

```bash
git checkout -b feature/my-change
# ... make changes ...
dotnet build AegisId.sln -warnaserror
dotnet format AegisId.sln --verify-no-changes
dotnet test AegisId.sln
git push -u origin feature/my-change
gh pr create
```

CI also runs a `dashboard` job (`ng build` + `ng test` in `web/`) — not
yet a required status check, but changes there should still pass it
locally before pushing:

```bash
cd web
npx ng build
npx ng test --no-watch --browsers=ChromeHeadless
```

## Adding a control

A control is a class implementing `IControl` in `Aegis.Controls` —
dropping the class in is enough, `ControlEngine.DiscoverFrom` finds it by
reflection at startup. Use an existing control (e.g.
`src/Aegis.Controls/Iam/GlobalAdminCountOutOfRangeControl.cs`) as the
template: `Id`, `Title`, `DefaultSeverity`, `CisReference`,
`MitreTechnique`, and an `Evaluate` method returning `Finding`s.

Each control needs:

1. At least one compliant and one non-compliant fixture snapshot under
   `tests/Aegis.Domain.Tests/Fixtures/iam-0XX-*.json`.
2. A test class under `tests/Aegis.Domain.Tests/Controls/`, following the
   naming and structure of an existing one.
3. A `docs/controls/IAM-0XX.md` entry: severity, CIS/MITRE references,
   logic, and known false positives.
4. If the finding depends on the current time (like credential expiry or
   staleness checks), take a `TimeProvider` in the constructor — see
   `ExpiringApplicationCredentialsControl` — so tests are deterministic.

## Conventions

- Nullable reference types and implicit usings are on everywhere; keep
  warnings at zero (`-warnaserror` is what CI runs).
- `dotnet format --verify-no-changes` must pass — run `dotnet format`
  locally before pushing if it doesn't.
- No secrets, ever, in code, fixtures, or commit messages — `gitleaks`
  scans every push and PR. `.gitignore` already excludes `*.env`,
  `appsettings.Local.json`, and `*.snapshot.json`.
- Domain layer (`Aegis.Domain`) has zero external dependencies by design —
  keep it that way. Infrastructure concerns (YAML, HTTP, JSON output
  formatting) belong in `Aegis.Graph` or `Aegis.Cli`.

## Definition of done

From `Aegis-ID_Product_Backlog.md` §9: merged via PR, tests pass with no
coverage regression, CI green including `gitleaks`, documentation updated
(including the control's `docs/controls/IAM-XXX.md` if applicable), no
`TODO` or commented-out code left in the diff.
