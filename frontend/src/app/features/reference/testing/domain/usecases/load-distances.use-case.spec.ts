import { TestBed } from '@angular/core/testing';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { LoadDistancesUseCase } from '@features/reference/domain/usecases/load-distances.use-case';

function setup(getDistances: jest.Mock) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      LoadDistancesUseCase,
      { provide: REFERENCE_REPOSITORY, useValue: { getDistances } },
    ],
  });
  return TestBed.inject(LoadDistancesUseCase);
}

describe('LoadDistancesUseCase', () => {
  it('maps a valid response to LookupItem[]', async () => {
    const getDistances = jest.fn().mockResolvedValue({
      data: [{ id: 'd1', code: '50m', nameEn: '50m', nameAr: '٥٠ متر' }],
    });
    const uc = setup(getDistances);

    const res = await uc.run();

    expect(res.ok).toBe(true);
    expect(res.ok && res.data).toEqual([{ id: 'd1', code: '50m', nameEn: '50m', nameAr: '٥٠ متر' }]);
  });

  it('fails with a validation error on a malformed payload', async () => {
    const getDistances = jest.fn().mockResolvedValue({ data: [{ id: 'd1' }] });
    const uc = setup(getDistances);

    const res = await uc.run();

    expect(res.ok).toBe(false);
  });
});
