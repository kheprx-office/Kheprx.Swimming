import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface GuardianDtoRs { id: string; relationCode: string; name: string; nationalId: string; phone: string; }
export interface SwimmerGuardiansDtoRs { father: GuardianDtoRs | null; mother: GuardianDtoRs | null; }
export interface GuardiansItemDtoRs extends BaseResponseRs<SwimmerGuardiansDtoRs> {}

export interface GuardianInputDtoRq { name: string; nationalId: string; phone: string; }
export interface UpsertGuardiansDtoRq { father: GuardianInputDtoRq; mother: GuardianInputDtoRq; }
export interface UpsertGuardiansItemDtoRs extends BaseResponseRs<unknown> {}

export function isSwimmerGuardiansDtoRsValid(dto: unknown): dto is SwimmerGuardiansDtoRs {
  const d = dto as SwimmerGuardiansDtoRs;
  if (!d || typeof d !== 'object') return false;
  const slotOk = (s: GuardianDtoRs | null) =>
    s === null || (typeof s.id === 'string' && typeof s.name === 'string' && typeof s.relationCode === 'string');
  return slotOk(d.father) && slotOk(d.mother);
}
