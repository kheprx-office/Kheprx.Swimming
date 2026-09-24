// reference.repository.ts — port + DI token.
import { InjectionToken } from '@angular/core';
import { ClubListDtoRs, CodedLookupListDtoRs } from '@features/reference/data/dto/reference.dto';

export interface IReferenceRepository {
  getClubs(): Promise<ClubListDtoRs>;
  getBloodTypes(): Promise<CodedLookupListDtoRs>;
  getStrokes(): Promise<CodedLookupListDtoRs>;
  getDistances(): Promise<CodedLookupListDtoRs>;
  getGenders(): Promise<CodedLookupListDtoRs>;
  getObservationCategories(): Promise<CodedLookupListDtoRs>;
  getFeedbackCategories(): Promise<CodedLookupListDtoRs>;
  getFitnessAssessments(): Promise<CodedLookupListDtoRs>;
  getAttendanceStatuses(): Promise<CodedLookupListDtoRs>;
}

export const REFERENCE_REPOSITORY = new InjectionToken<IReferenceRepository>('REFERENCE_REPOSITORY');
