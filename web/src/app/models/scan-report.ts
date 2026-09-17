export type Severity = 'critical' | 'high' | 'medium' | 'low' | 'info';

export type ControlStatus = 'passed' | 'failed' | 'skipped' | 'error';

export interface Finding {
  controlId: string;
  severity: Severity;
  objectType: string;
  objectId: string;
  objectName: string;
  evidence: string;
  riskDescription: string;
  remediation: string[];
  cisReference: string | null;
  mitreTechnique: string | null;
  detectedAt: string;
  isExpectedException: boolean;
  suppressionReason: string | null;
  isSuppressed: boolean;
}

export interface ControlReport {
  controlId: string;
  title: string;
  status: ControlStatus;
  skipReason: string | null;
  errorMessage: string | null;
  findings: Finding[];
}

export interface SeverityCounts {
  critical: number;
  high: number;
  medium: number;
  low: number;
  info: number;
}

export interface ScanReport {
  schemaVersion: string;
  tenant: string;
  evaluatedAt: string;
  postureScore: number;
  severityCounts: SeverityCounts;
  controls: ControlReport[];
}

/** A finding flattened with its control title, and a stable id for routing/selection. */
export interface FlatFinding extends Finding {
  id: string;
  controlTitle: string;
}

export const SEVERITY_ORDER: Severity[] = ['critical', 'high', 'medium', 'low', 'info'];

export const SEVERITY_LABEL: Record<Severity, string> = {
  critical: 'Critical',
  high: 'High',
  medium: 'Medium',
  low: 'Low',
  info: 'Info',
};
