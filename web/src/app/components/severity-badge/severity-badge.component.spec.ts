import { TestBed } from '@angular/core/testing';

import { SeverityBadgeComponent } from './severity-badge.component';

describe('SeverityBadgeComponent', () => {
  it('renders the human-readable label for each severity, never color alone', async () => {
    const fixture = TestBed.createComponent(SeverityBadgeComponent);
    fixture.componentInstance.severity = 'critical';
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Critical');

    // The color swatch is decorative only — text is the accessible carrier of meaning.
    const dot = fixture.nativeElement.querySelector('.severity-badge__dot');
    expect(dot?.getAttribute('aria-hidden')).toBe('true');
  });
});
