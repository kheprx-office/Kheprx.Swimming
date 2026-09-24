// reference.repository.impl.ts — fetch-only reference repository (/api/reference/*).
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IReferenceRepository } from '@features/reference/domain/repositories/reference.repository';
import { ClubListDtoRs, CodedLookupListDtoRs } from '@features/reference/data/dto/reference.dto';

@Injectable({ providedIn: 'root' })
export class ReferenceRepositoryImpl implements IReferenceRepository {
  private readonly http = inject(HttpClientService);

  getClubs(): Promise<ClubListDtoRs> {
    return this.http.get<ClubListDtoRs>('/api/reference/clubs');
  }
  getBloodTypes(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/blood-types');
  }
  getStrokes(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/strokes');
  }
  getDistances(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/distances');
  }
  getGenders(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/genders');
  }
  getObservationCategories(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/observation-categories');
  }
  getFeedbackCategories(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/feedback-categories');
  }
  getFitnessAssessments(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/fitness-assessments');
  }
  getAttendanceStatuses(): Promise<CodedLookupListDtoRs> {
    return this.http.get<CodedLookupListDtoRs>('/api/reference/attendance-statuses');
  }
}
