import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ScanReport } from '../models/scan-report';
import { ScanService } from './scan.service';

const SAMPLE_REPORT: ScanReport = {
  schemaVersion: '1.0',
  tenant: 'Fixture Tenant',
  evaluatedAt: '2026-01-01T00:00:00Z',
  postureScore: 85,
  severityCounts: { critical: 0, high: 1, medium: 0, low: 0, info: 0 },
  controls: [
    {
      controlId: 'IAM-001',
      title: 'Test control',
      status: 'failed',
      skipReason: null,
      errorMessage: null,
      findings: [
        {
          controlId: 'IAM-001',
          severity: 'high',
          objectType: 'User',
          objectId: 'u-1',
          objectName: 'alice@fixture.test',
          evidence: 'evidence',
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

describe('ScanService', () => {
  let service: ScanService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(ScanService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loadDemo populates the report and flattens findings with a stable id', () => {
    service.loadDemo();
    httpMock.expectOne((req) => req.url.endsWith('/api/scan/demo')).flush(SAMPLE_REPORT);

    expect(service.report()).toEqual(SAMPLE_REPORT);
    expect(service.loading()).toBeFalse();
    expect(service.findings().length).toBe(1);
    expect(service.findings()[0].id).toBe('IAM-001::u-1::0');
    expect(service.findings()[0].controlTitle).toBe('Test control');
  });

  it('loadDemo sets a readable error message when the API is unreachable', () => {
    service.loadDemo();
    httpMock.expectOne((req) => req.url.endsWith('/api/scan/demo')).error(new ProgressEvent('network error'), { status: 0 });

    expect(service.error()).toContain('Could not reach');
    expect(service.report()).toBeNull();
  });
});
