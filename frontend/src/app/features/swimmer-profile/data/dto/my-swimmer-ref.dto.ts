import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface MySwimmerRefDtoRs { swimmerId: string; }
export interface MySwimmerRefItemDtoRs extends BaseResponseRs<MySwimmerRefDtoRs> {}

export function isMySwimmerRefValid(dto: unknown): dto is MySwimmerRefDtoRs {
  const d = dto as MySwimmerRefDtoRs;
  return !!d && typeof d.swimmerId === 'string' && d.swimmerId.length > 0;
}
