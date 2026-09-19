import { BaseResponseRs } from '@core/network/api/base-response-rs';

export interface UpdateIdentityDtoRq { nameEn: string; nameAr?: string | null; dob: string; phone?: string | null; }
export interface UpdateIdentityItemDtoRs extends BaseResponseRs<unknown> {}
