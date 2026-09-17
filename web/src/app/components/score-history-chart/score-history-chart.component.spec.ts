import { TestBed } from '@angular/core/testing';

import { ScanHistoryEntry } from '../../models/scan-report';
import { ScoreHistoryChartComponent } from './score-history-chart.component';

describe('ScoreHistoryChartComponent', () => {
  function createWithEntries(entries: ScanHistoryEntry[]) {
    const fixture = TestBed.createComponent(ScoreHistoryChartComponent);
    fixture.componentRef.setInput('entries', entries);
    fixture.detectChanges();
    return fixture;
  }

  const entry = (score: number, hoursAgo: number): ScanHistoryEntry => ({
    id: `scan-${hoursAgo}`,
    evaluatedAt: new Date(Date.now() - hoursAgo * 3_600_000).toISOString(),
    tenant: 'Fixture Tenant',
    postureScore: score,
  });

  it('shows the empty state with fewer than two scans', () => {
    const fixture = createWithEntries([entry(80, 1)]);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Not enough history yet');
    expect(fixture.nativeElement.querySelector('svg')).toBeNull();
  });

  it('renders the chart with two or more scans', () => {
    const fixture = createWithEntries([entry(40, 2), entry(70, 1)]);

    expect(fixture.nativeElement.querySelector('svg')).not.toBeNull();
    expect(fixture.componentInstance.points().length).toBe(2);
  });

  // Regression test: a score of 100 previously put the end-label's y coordinate
  // (last.y - 10) above the SVG viewBox (y < 0), clipping it at the top.
  it('keeps every point, including a score of 100, within the SVG viewBox', () => {
    const fixture = createWithEntries([entry(50, 2), entry(100, 1)]);
    const component = fixture.componentInstance;

    for (const point of component.points()) {
      expect(point.y).toBeGreaterThanOrEqual(0);
    }

    const last = component.lastPoint()!;
    expect(last.y - 10).toBeGreaterThanOrEqual(0);
  });
});
