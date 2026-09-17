import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject } from '@angular/core';
import { Router } from '@angular/router';

import { SeverityBadgeComponent } from '../../components/severity-badge/severity-badge.component';
import { SEVERITY_ORDER, Severity } from '../../models/scan-report';
import { ScanService } from '../../services/scan.service';

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [SeverityBadgeComponent, DatePipe],
  templateUrl: './overview.component.html',
  styleUrl: './overview.component.css',
})
export class OverviewComponent implements OnInit {
  private readonly scanService = inject(ScanService);
  private readonly router = inject(Router);

  readonly report = this.scanService.report;
  readonly loading = this.scanService.loading;
  readonly error = this.scanService.error;
  readonly severityOrder = SEVERITY_ORDER;

  readonly controlCounts = computed(() => {
    const report = this.report();
    const counts = { passed: 0, failed: 0, skipped: 0, error: 0 };
    if (!report) return counts;

    for (const control of report.controls) {
      counts[control.status]++;
    }
    return counts;
  });

  ngOnInit(): void {
    if (!this.report()) {
      this.scanService.loadDemo();
    }
  }

  retry(): void {
    this.scanService.loadDemo();
  }

  severityCount(severity: Severity): number {
    return this.report()?.severityCounts[severity] ?? 0;
  }

  goToSeverity(severity: Severity): void {
    this.router.navigate(['/findings'], { queryParams: { severity } });
  }
}
