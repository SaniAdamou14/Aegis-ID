import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap } from 'rxjs';

import { API_BASE_URL } from '../config';
import { FlatFinding, ScanHistoryEntry, ScanReport } from '../models/scan-report';

@Injectable({ providedIn: 'root' })
export class ScanService {
  private readonly http = inject(HttpClient);

  private readonly _report = signal<ScanReport | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _history = signal<ScanHistoryEntry[]>([]);

  readonly report = this._report.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly history = this._history.asReadonly();

  /** All findings across all controls, flattened with a stable id for routing/selection. */
  readonly findings = computed<FlatFinding[]>(() => {
    const report = this._report();
    if (!report) return [];

    return report.controls.flatMap((control) =>
      control.findings.map((finding, index) => ({
        ...finding,
        controlTitle: control.title,
        id: `${finding.controlId}::${finding.objectId}::${index}`,
      })),
    );
  });

  loadDemo(): void {
    this._loading.set(true);
    this._error.set(null);

    this.http
      .get<ScanReport>(`${API_BASE_URL}/api/scan/demo`)
      .pipe(
        tap((report) => this._report.set(report)),
        catchError((err) => {
          this._error.set(
            err?.status === 0
              ? 'Could not reach the Aegis-ID API. Is it running?'
              : `The API returned an error (${err?.status ?? 'unknown'}).`,
          );
          return of(null);
        }),
      )
      .subscribe(() => this._loading.set(false));
  }

  /** Best-effort: an empty/missing history is a normal state (the chart shows its own empty state), not an error to surface. */
  loadHistory(limit = 10): void {
    this.http
      .get<ScanHistoryEntry[]>(`${API_BASE_URL}/api/scans/history`, { params: { limit } })
      .pipe(
        tap((entries) => this._history.set(entries)),
        catchError(() => {
          this._history.set([]);
          return of(null);
        }),
      )
      .subscribe();
  }
}
