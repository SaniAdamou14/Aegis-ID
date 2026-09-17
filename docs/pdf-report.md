# PDF audit report (`--output pdf`)

US-012. Generates a client-ready PDF: cover page, executive summary
(posture score + a findings-by-severity bar chart), findings detail
(a table, one row per finding, with the header repeated on every page),
and a methodology appendix.

```bash
aegis evaluate --from snapshot.json --output pdf --file report.pdf
aegis scan --tenant-id <id> --client-id <id> --output pdf --file report.pdf
```

Built with [QuestPDF](https://www.questpdf.com/) (Community license — free
for organizations with under $1M USD annual gross revenue, which covers an
open-source/personal project like this one; see QuestPDF's own license
terms if you fork this for a larger organization).

## Branding

By default the cover page says "Aegis-ID". To put your own firm's name and
logo on it instead, pass `--branding <file.json>`:

```json
{
  "firmName": "Acme Security Consulting",
  "logoPath": "C:\\path\\to\\logo.png"
}
```

Both fields are optional. `logoPath` is a local file path (PNG/JPEG); if it
doesn't exist, the logo is silently skipped rather than failing the report.

## Performance

A 200-finding report generates in well under the 10-second budget (US-012's
acceptance criterion) — see
`tests/Aegis.Cli.Tests/PdfReporterTests.cs`'s timing test, which fails the
build if that regresses.
