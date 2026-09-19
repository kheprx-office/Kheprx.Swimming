import { TestBed } from '@angular/core/testing';
import { CreateBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/create-body-measurement.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('CreateBodyMeasurementUseCase', () => {
  const repo = { createBodyMeasurement: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CreateBodyMeasurementUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('calls the repository and succeeds', async () => {
    repo.createBodyMeasurement.mockResolvedValue({ successStatus: true, data: null });
    const rq = { rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 };
    const res = await TestBed.inject(CreateBodyMeasurementUseCase).run({ id: 'sw1', rq });
    expect(res.ok).toBe(true);
    expect(repo.createBodyMeasurement).toHaveBeenCalledWith('sw1', rq);
  });
});
