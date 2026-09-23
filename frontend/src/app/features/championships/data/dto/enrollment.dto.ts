import { BaseResponseRs } from '@core/network/api/base-response-rs';

/** Replace-the-whole-set request for an event's enrollment. */
export interface SetEnrollmentsRq {
  swimmerIds: string[];
}

/** GET enrollments returns the enrolled swimmer ids (Guids serialize as strings). */
export interface EnrollmentIdsDtoRs extends BaseResponseRs<string[]> {}

/** PUT enrollments replaces the whole set and returns ApiResponse<object> — data is null; no ids echoed back. */
export interface EnrollmentSaveDtoRs extends BaseResponseRs<unknown> {}

export function isEnrollmentIdsValid(data: unknown): data is string[] {
  return Array.isArray(data) && data.every((x) => typeof x === 'string');
}
