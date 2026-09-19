import { toLatestBodyMeasurement } from '@features/swimmer-profile/data/dto/body-measurement.mapper';
import { SwimmerBodyMeasurementDtoRs } from '@features/swimmer-profile/data/dto/body-measurement.dto';

describe('toLatestBodyMeasurement', () => {
  it('maps a present latest measurement', () => {
    const dto: SwimmerBodyMeasurementDtoRs = {
      latest: { id: 'b1', measuredAt: '2026-09-19', rightArmCm: 78.5, leftArmCm: 78.2, rightLegCm: 96.2, leftLegCm: 96.0, torsoCm: 52.8, bustDiameterCm: 94.0, waistDiameterCm: 76.5 },
    };
    const m = toLatestBodyMeasurement(dto);
    expect(m?.rightArmCm).toBe(78.5);
    expect(m?.waistDiameterCm).toBe(76.5);
    expect(m?.measuredAt).toBe('2026-09-19');
  });

  it('returns null when latest is null', () => {
    expect(toLatestBodyMeasurement({ latest: null })).toBeNull();
  });
});
