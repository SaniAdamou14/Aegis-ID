import { Component, OnInit, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';

import { FindingDetailComponent } from '../../components/finding-detail/finding-detail.component';
import { SeverityBadgeComponent } from '../../components/severity-badge/severity-badge.component';
import { FlatFinding, SEVERITY_LABEL, SEVERITY_ORDER, Severity } from '../../models/scan-report';
import { ScanService } from '../../services/scan.service';

const PAGE_SIZE = 25;

@Component({
  selector: 'app-findings',
  standalone: true,
  imports: [SeverityBadgeComponent, FindingDetailComponent],
  templateUrl: './findings.component.html',
  styleUrl: './findings.component.css',
})
export class FindingsComponent implements OnInit {
  private readonly scanService = inject(ScanService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = this.scanService.loading;
  readonly error = this.scanService.error;
  readonly severityOrder = SEVERITY_ORDER;
  readonly severityLabel = SEVERITY_LABEL;

  private readonly queryParams = toSignal(this.route.queryParamMap, { requireSync: true });

  readonly severityFilter = computed(() => this.queryParams().get('severity') as Severity | null);
  readonly controlFilter = computed(() => this.queryParams().get('control'));
  readonly objectTypeFilter = computed(() => this.queryParams().get('objectType'));
  readonly searchQuery = computed(() => this.queryParams().get('q') ?? '');
  readonly page = computed(() => Math.max(1, Number(this.queryParams().get('page')) || 1));
  readonly selectedId = computed(() => this.queryParams().get('selected'));

  readonly availableControls = computed(() =>
    Array.from(new Set(this.scanService.findings().map((f) => f.controlId))).sort(),
  );

  readonly availableObjectTypes = computed(() =>
    Array.from(new Set(this.scanService.findings().map((f) => f.objectType))).sort(),
  );

  readonly hasActiveFilters = computed(
    () => !!(this.severityFilter() || this.controlFilter() || this.objectTypeFilter() || this.searchQuery()),
  );

  readonly filteredFindings = computed<FlatFinding[]>(() => {
    const severity = this.severityFilter();
    const control = this.controlFilter();
    const objectType = this.objectTypeFilter();
    const search = this.searchQuery().trim().toLowerCase();

    return this.scanService.findings().filter((f) => {
      if (severity && f.severity !== severity) return false;
      if (control && f.controlId !== control) return false;
      if (objectType && f.objectType !== objectType) return false;
      if (search && !f.objectName.toLowerCase().includes(search)) return false;
      return true;
    });
  });

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.filteredFindings().length / PAGE_SIZE)));

  readonly pagedFindings = computed(() => {
    const page = Math.min(this.page(), this.totalPages());
    const start = (page - 1) * PAGE_SIZE;
    return this.filteredFindings().slice(start, start + PAGE_SIZE);
  });

  readonly selectedFinding = computed(
    () => this.scanService.findings().find((f) => f.id === this.selectedId()) ?? null,
  );

  ngOnInit(): void {
    if (!this.scanService.report()) {
      this.scanService.loadDemo();
    }
  }

  onSeverityChange(value: string): void {
    this.updateFilter('severity', value);
  }

  onControlChange(value: string): void {
    this.updateFilter('control', value);
  }

  onObjectTypeChange(value: string): void {
    this.updateFilter('objectType', value);
  }

  onSearchInput(value: string): void {
    this.updateFilter('q', value);
  }

  private updateFilter(key: string, value: string | null): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { [key]: value || null, page: null, selected: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  clearFilters(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  goToPage(page: number): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  select(finding: FlatFinding): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { selected: finding.id },
      queryParamsHandling: 'merge',
    });
  }

  closeDetail(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { selected: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
