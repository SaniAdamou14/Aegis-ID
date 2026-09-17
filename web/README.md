# Aegis-ID dashboard

The Angular 19 dashboard for [Aegis-ID](../README.md) — posture overview,
filterable findings list, finding detail panel, score-over-time chart.
Talks to `Aegis.Api` over HTTP; see the root README's
[Dashboard](../README.md#dashboard) section for how to run both together.

Generated with [Angular CLI](https://github.com/angular/angular-cli) 19.2.9.

## Development server

```bash
npm start   # equivalent to `ng serve` — http://localhost:4200
```

Requires `Aegis.Api` running separately (`dotnet run --project ../src/Aegis.Api`)
unless you use the root `npm run dev`, which starts both.

## Build

```bash
npm run build   # equivalent to `ng build` — output in dist/dashboard/browser
```

## Tests

```bash
npx ng test --no-watch --browsers=ChromeHeadless
```

No end-to-end test framework is set up — `ng e2e` isn't available here.
