import { TestBed } from '@angular/core/testing';
import { GetLatestBodyMeasurementUseCase } from '@features/swimmer-profile/domain/usecases/get-latest-body-measurement.use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

describe('GetLatestBodyMeasurementUseCase', () => {
  const repo = { getBodyMeasurement: jest.fn() } as any;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GetLatestBodyMeasurementUseCase, { provide: SWIMMER_PROFILE_REPOSITORY, useValue: repo }],
    });
  });

  it('maps a present latest measurement', async () => {
    repo.getBodyMeasurement.mockResolvedValue({ successStatus: true, data: {
      latest: { id: 'b1', measuredAt: '2026-09-19', rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 } } });
    const res = await TestBed.inject(GetLatestBodyMeasurementUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data?.rightArmCm).toBe(78.5);
  });

  it('returns null data when there is no measurement', async () => {
    repo.getBodyMeasurement.mockResolvedValue({ successStatus: true, data: { latest: null } });
    const res = await TestBed.inject(GetLatestBodyMeasurementUseCase).run('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toBeNull();
  });

  it('fails on an invalid response', async () => {
    repo.getBodyMeasurement.mockResolvedValue({ successStatus: true, data: { latest: { id: 5 } } });
    const res = await TestBed.inject(GetLatestBodyMeasurementUseCase).run('sw1');
    expect(res.ok).toBe(false);
  });
});
