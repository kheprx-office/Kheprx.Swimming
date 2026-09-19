import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface InBodyReadingDtoRs {
  id: string;
  readingDate: string;
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  boneDensity: number;
  bodyDensity: number;
  recordedBy: string;
}
export interface InBodyReadingListDtoRs extends BaseResponseRs<InBodyReadingDtoRs[]> {}
export interface InBodyReadingItemDtoRs extends BaseResponseRs<InBodyReadingDtoRs> {}
export interface DeleteInBodyReadingItemDtoRs extends BaseResponseRs<unknown> {}

export interface CreateInBodyReadingDtoRq {
  readingDate: string;
  heightCm: number;
  weightKg: number;
  fatPct: number;
  musclePct: number;
  boneDensity: number;
  bodyDensity: number;
}

const VALUE_KEYS = ['heightCm', 'weightKg', 'fatPct', 'musclePct', 'boneDensity', 'bodyDensity'] as const;

export function isInBodyReadingDtoRsValid(x: unknown): x is InBodyReadingDtoRs {
  const d = x as InBodyReadingDtoRs;
  if (!d || typeof d !== 'object') return false;
  if (typeof d.id !== 'string' || typeof d.readingDate !== 'string') return false;
  return VALUE_KEYS.every((k) => typeof (d as unknown as Record<string, unknown>)[k] === 'number');
}

export function isInBodyReadingListValid(data: unknown): data is InBodyReadingDtoRs[] {
  return Array.isArray(data) && data.every(isInBodyReadingDtoRsValid);
}
