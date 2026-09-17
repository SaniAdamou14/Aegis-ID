import { DatePipe } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';

import { ScanHistoryEntry } from '../../models/scan-report';

@Component({
  selector: 'app-score-history-chart',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './score-history-chart.component.html',
  styleUrl: './score-history-chart.component.css',
})
export class ScoreHistoryChartComponent {
  entries = input.required<ScanHistoryEntry[]>();

  readonly width = 600;
  readonly height = 200;
  private readonly paddingLeft = 34;
  private readonly paddingRight = 20;
  // Tall enough to fit the end-label above the topmost point when the score is 100 — it sits at `last.y - 10`.
  private readonly paddingTop = 28;
  private readonly paddingBottom = 8;

  readonly hoveredIndex = signal<number | null>(null);

  readonly points = computed(() => {
    const entries = this.entries();
    const innerWidth = this.width - this.paddingLeft - this.paddingRight;
    const innerHeight = this.height - this.paddingTop - this.paddingBottom;
    const n = entries.length;

    return entries.map((entry, i) => ({
      x: this.paddingLeft + (n === 1 ? innerWidth / 2 : (i / (n - 1)) * innerWidth),
      y: this.paddingTop + innerHeight - (entry.postureScore / 100) * innerHeight,
      entry,
    }));
  });

  readonly linePath = computed(() =>
    this.points()
      .map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`)
      .join(' '),
  );

  readonly areaPath = computed(() => {
    const pts = this.points();
    if (pts.length === 0) return '';

    const baseline = this.height - this.paddingBottom;
    return (
      `M ${pts[0].x.toFixed(1)} ${baseline} ` +
      pts.map((p) => `L ${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ') +
      ` L ${pts[pts.length - 1].x.toFixed(1)} ${baseline} Z`
    );
  });

  readonly gridLines = computed(() => {
    const innerHeight = this.height - this.paddingTop - this.paddingBottom;
    return [0, 50, 100].map((value) => ({
      y: this.paddingTop + innerHeight - (value / 100) * innerHeight,
      label: String(value),
    }));
  });

  readonly hoveredPoint = computed(() => {
    const i = this.hoveredIndex();
    return i === null ? null : (this.points()[i] ?? null);
  });

  readonly lastPoint = computed(() => {
    const pts = this.points();
    return pts.length > 0 ? pts[pts.length - 1] : null;
  });

  onHover(index: number): void {
    this.hoveredIndex.set(index);
  }

  onLeave(): void {
    this.hoveredIndex.set(null);
  }
}
