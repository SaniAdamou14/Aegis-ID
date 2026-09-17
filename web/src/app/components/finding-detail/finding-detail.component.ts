import { DatePipe } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Input, OnChanges, Output, ViewChild, signal } from '@angular/core';

import { SeverityBadgeComponent } from '../severity-badge/severity-badge.component';
import { FlatFinding } from '../../models/scan-report';

@Component({
  selector: 'app-finding-detail',
  standalone: true,
  imports: [SeverityBadgeComponent, DatePipe],
  templateUrl: './finding-detail.component.html',
  styleUrl: './finding-detail.component.css',
})
export class FindingDetailComponent implements OnChanges {
  @Input({ required: true }) finding!: FlatFinding;
  @Output() close = new EventEmitter<void>();

  @ViewChild('closeButton') closeButton?: ElementRef<HTMLButtonElement>;

  readonly copied = signal(false);

  get controlDocUrl(): string {
    return `https://github.com/SaniAdamou14/Aegis-ID/blob/master/docs/controls/${this.finding.controlId}.md`;
  }

  get cisUrl(): string {
    return 'https://www.cisecurity.org/benchmark/microsoft_365';
  }

  get mitreUrl(): string | null {
    const technique = this.finding.mitreTechnique;
    if (!technique) return null;

    const [base, sub] = technique.split('.');
    return sub
      ? `https://attack.mitre.org/techniques/${base}/${sub}/`
      : `https://attack.mitre.org/techniques/${base}/`;
  }

  ngOnChanges(): void {
    this.copied.set(false);
    queueMicrotask(() => this.closeButton?.nativeElement.focus());
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close.emit();
  }

  async copyEvidence(): Promise<void> {
    const { id, controlTitle, ...finding } = this.finding;
    await navigator.clipboard.writeText(JSON.stringify(finding, null, 2));
    this.copied.set(true);
  }
}
