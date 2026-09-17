import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { routes } from '../../app.routes';
import { ScanReport } from '../../models/scan-report';
import { FindingsComponent } from './findings.component';

const SAMPLE_REPORT: ScanReport = {
  schemaVersion: '1.0',
  tenant: 'Fixture Tenant',
  evaluatedAt: '2026-01-01T00:00:00Z',
  postureScore: 50,
  severityCounts: { critical: 1, high: 1, medium: 0, low: 0, info: 0 },
  controls: [
    {
      controlId: 'IAM-001',
      title: 'Control A',
      status: 'failed',
      skipReason: null,
      errorMessage: null,
      findings: [
        {
          controlId: 'IAM-001',
          severity: 'critical',
          objectType: 'User',
          objectId: 'u-1',
          objectName: 'alice@fixture.test',
          evidence: 'evidence A',
          riskDescription: 'risk',
          remediation: ['step'],
          cisReference: null,
          mitreTechnique: null,
          detectedAt: '2026-01-01T00:00:00Z',
          isExpectedException: false,
          suppressionReason: null,
          isSuppressed: false,
        },
      ],
    },
    {
      controlId: 'IAM-002',
      title: 'Control B',
      status: 'failed',
      skipReason: null,
      errorMessage: null,
      findings: [
        {
          controlId: 'IAM-002',
          severity: 'high',
          objectType: 'Application',
          objectId: 'app-1',
          objectName: 'Some-App',
          evidence: 'evidence B',
          riskDescription: 'risk',
          remediation: ['step'],
          cisReference: null,
          mitreTechnique: null,
          detectedAt: '2026-01-01T00:00:00Z',
          isExpectedException: false,
          suppressionReason: null,
          isSuppressed: false,
        },
      ],
    },
  ],
};

describe('FindingsComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [provideRouter(routes)],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('reads the severity filter from the URL and filters the findings accordingly', async () => {
    const harness = await RouterTestingHarness.create('/findings?severity=high');
    httpMock.expectOne((req) => req.url.endsWith('/api/scan/demo')).flush(SAMPLE_REPORT);
    harness.detectChanges();

    const component = harness.routeDebugElement!.componentInstance as FindingsComponent;

    expect(component.severityFilter()).toBe('high');
    expect(component.filteredFindings().length).toBe(1);
    expect(component.filteredFindings()[0].objectName).toBe('Some-App');
  });

  it('with no filters, lists every finding', async () => {
    const harness = await RouterTestingHarness.create('/findings');
    httpMock.expectOne((req) => req.url.endsWith('/api/scan/demo')).flush(SAMPLE_REPORT);
    harness.detectChanges();

    const component = harness.routeDebugElement!.componentInstance as FindingsComponent;

    expect(component.filteredFindings().length).toBe(2);
  });
});
