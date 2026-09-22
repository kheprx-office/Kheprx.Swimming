import { TestBed } from '@angular/core/testing';
import { ListHealthReadingsUseCase } from '@features/health-readings/domain/usecases/list-health-readings.use-case';
import { UpdateHealthReadingUseCase } from '@features/health-readings/domain/usecases/update-health-reading.use-case';
import { DeleteHealthReadingUseCase } from '@features/health-readings/domain/usecases/delete-health-reading.use-case';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';

const ROW = {
  id: 'r1', medicalTestId: 't1', testNameEn: 'Glucose', testNameAr: 'الجلوكوز', unit: 'mg/dL',
  value: 90, lowerBound: 70, upperBound: 110, readingDate: '2024-10-04T00:00:00Z', status: 'normal',
};

function setup() {
  const repo = { create: jest.fn(), list: jest.fn(), update: jest.fn(), remove: jest.fn() };
  TestBed.configureTestingModule({ providers: [{ provide: HEALTH_READING_REPOSITORY, useValue: repo }] });
  return { repo };
}

describe('health-reading CRUD use-cases', () => {
  it('list maps valid rows', async () => {
    const { repo } = setup();
    repo.list.mockResolvedValue({ data: [ROW] });
    const res = await TestBed.inject(ListHealthReadingsUseCase).run('s1');
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data).toHaveLength(1); expect(res.data[0].testNameEn).toBe('Glucose'); }
  });

  it('list fails validation for a malformed row', async () => {
    const { repo } = setup();
    repo.list.mockResolvedValue({ data: [{ id: 'r1' }] });
    const res = await TestBed.inject(ListHealthReadingsUseCase).run('s1');
    expect(res.ok).toBe(false);
  });

  it('update sends value and maps the row', async () => {
    const { repo } = setup();
    repo.update.mockResolvedValue({ data: { ...ROW, value: 40, status: 'out' } });
    const res = await TestBed.inject(UpdateHealthReadingUseCase).run({ id: 'r1', rq: { value: 40 } });
    expect(repo.update).toHaveBeenCalledWith('r1', { value: 40 });
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data.value).toBe(40); expect(res.data.status).toBe('out'); }
  });

  it('delete calls remove', async () => {
    const { repo } = setup();
    repo.remove.mockResolvedValue({ data: null });
    const res = await TestBed.inject(DeleteHealthReadingUseCase).run({ id: 'r1' });
    expect(repo.remove).toHaveBeenCalledWith('r1');
    expect(res.ok).toBe(true);
  });
});
