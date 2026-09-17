import { Component, Input } from '@angular/core';

import { SEVERITY_LABEL, Severity } from '../../models/scan-report';

@Component({
  selector: 'app-severity-badge',
  standalone: true,
  template: `
    <span class="severity-badge" [class]="'severity-badge--' + severity">
      <span class="severity-badge__dot" aria-hidden="true"></span>
      {{ label }}
    </span>
  `,
  styleUrl: './severity-badge.component.css',
})
export class SeverityBadgeComponent {
  @Input({ required: true }) severity!: Severity;

  get label(): string {
    return SEVERITY_LABEL[this.severity];
  }
}
