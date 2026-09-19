import { InBodyReadingDtoRs } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';

export function toInBodyReading(d: InBodyReadingDtoRs): InBodyReading {
  return {
    id: d.id,
    readingDate: d.readingDate,
    heightCm: d.heightCm,
    weightKg: d.weightKg,
    fatPct: d.fatPct,
    musclePct: d.musclePct,
    waterPct: d.waterPct,
    boneDensity: d.boneDensity,
    bodyDensity: d.bodyDensity,
  };
}

export function toInBodyReadingList(list: InBodyReadingDtoRs[]): InBodyReading[] {
  return list.map(toInBodyReading);
}
