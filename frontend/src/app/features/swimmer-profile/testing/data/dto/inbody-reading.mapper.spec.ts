import { toInBodyReading, toInBodyReadingList } from '@features/swimmer-profile/data/dto/inbody-reading.mapper';
import { InBodyReadingDtoRs } from '@features/swimmer-profile/data/dto/inbody-reading.dto';

const DTO: InBodyReadingDtoRs = {
  id: 'r1', readingDate: '2024-10-04', heightCm: 180, weightKg: 74, fatPct: 12.8, musclePct: 42.1,
  waterPct: 55.3, boneDensity: 1.35, bodyDensity: 1.07, recordedBy: 'u1',
};

describe('inbody-reading.mapper', () => {
  it('maps a reading (drops recordedBy)', () => {
    const m = toInBodyReading(DTO);
    expect(m.id).toBe('r1');
    expect(m.readingDate).toBe('2024-10-04');
    expect(m.weightKg).toBe(74);
    expect(m.bodyDensity).toBe(1.07);
    expect((m as unknown as Record<string, unknown>).recordedBy).toBeUndefined();
  });

  it('maps a list', () => {
    expect(toInBodyReadingList([DTO])).toHaveLength(1);
  });
});
