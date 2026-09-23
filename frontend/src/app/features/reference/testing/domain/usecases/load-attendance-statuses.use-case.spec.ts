import { TestBed } from '@angular/core/testing';
import { LoadAttendanceStatusesUseCase } from '@features/reference/domain/usecases/load-attendance-statuses.use-case';
import { REFERENCE_REPOSITORY, IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';

function build(repo: IReferenceRepository) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: REFERENCE_REPOSITORY, useValue: repo }] });
  return TestBed.inject(LoadAttendanceStatusesUseCase);
}

describe('LoadAttendanceStatusesUseCase', () => {
  it('maps coded lookups to LookupItem[]', async () => {
    const repo = { getAttendanceStatuses: async () => ({ data: [{ id: 's1', code: 'present', nameEn: 'Present', nameAr: 'حاضر' }] }) } as unknown as IReferenceRepository;
    const uc = build(repo);
    const res = await uc.run();
    expect(res.ok).toBe(true);
    if (res.ok) {
      expect(res.data[0].id).toBe('s1');
      expect(res.data[0].code).toBe('present');
      expect(res.data[0].nameEn).toBe('Present');
    }
  });

  it('fails validation on malformed data', async () => {
    const repo = { getAttendanceStatuses: async () => ({ data: [{ id: 's1' }] }) } as unknown as IReferenceRepository;
    const uc = build(repo);
    const res = await uc.run();
    expect(res.ok).toBe(false);
    if (!res.ok) expect(res.error.kind).toBe('validation');
  });
});
