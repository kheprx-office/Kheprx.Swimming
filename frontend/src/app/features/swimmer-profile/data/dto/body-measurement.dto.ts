import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface BodyMeasurementDtoRs {
  id: string;
  measuredAt: string;
  rightArmCm: number;
  leftArmCm: number;
  rightLegCm: number;
  leftLegCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
export interface SwimmerBodyMeasurementDtoRs { latest: BodyMeasurementDtoRs | null; }
export interface BodyMeasurementItemDtoRs extends BaseResponseRs<SwimmerBodyMeasurementDtoRs> {}

export interface CreateBodyMeasurementDtoRq {
  rightArmCm: number;
  leftArmCm: number;
  rightLegCm: number;
  leftLegCm: number;
  torsoCm: number;
  bustDiameterCm: number;
  waistDiameterCm: number;
}
export interface CreateBodyMeasurementItemDtoRs extends BaseResponseRs<unknown> {}

const VALUE_KEYS = ['rightArmCm', 'leftArmCm', 'rightLegCm', 'leftLegCm', 'torsoCm', 'bustDiameterCm', 'waistDiameterCm'] as const;

export function isSwimmerBodyMeasurementDtoRsValid(dto: unknown): dto is SwimmerBodyMeasurementDtoRs {
  const d = dto as SwimmerBodyMeasurementDtoRs;
  if (!d || typeof d !== 'object') return false;
  const m = d.latest;
  if (m === null) return true;
  if (typeof m.id !== 'string') return false;
  return VALUE_KEYS.every((k) => typeof (m as unknown as Record<string, unknown>)[k] === 'number');
}
