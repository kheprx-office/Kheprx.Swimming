// swimmer-count.dto.ts — swimmer count response DTO (API_FLOW convention).
import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface SwimmerCountDtoRs {
  count: number;
}

export interface SwimmerCountItemDtoRs extends BaseResponseRs<SwimmerCountDtoRs> {}

export function isSwimmerCountDtoRsValid(dto: SwimmerCountDtoRs): boolean {
  return typeof dto.count === 'number' && Number.isFinite(dto.count) && dto.count >= 0;
}
