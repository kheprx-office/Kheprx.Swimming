// swimmer-profile.repository.impl.ts — profile read + identity/vitals writes.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { SwimmerProfileItemDtoRs, MedicalExamListDtoRs, DeleteExamItemDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { UpdateIdentityDtoRq, UpdateIdentityItemDtoRs } from '@features/swimmer-profile/data/dto/update-identity.dto';
import { CreateMedicalExamDtoRq, CreatedVitalsItemDtoRs } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';
import { GuardiansItemDtoRs, UpsertGuardiansDtoRq, UpsertGuardiansItemDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';

@Injectable({ providedIn: 'root' })
export class SwimmerProfileRepositoryImpl implements ISwimmerProfileRepository {
  private readonly http = inject(HttpClientService);

  getProfile(id: string): Promise<SwimmerProfileItemDtoRs> {
    return this.http.get<SwimmerProfileItemDtoRs>(`/api/swimmers/${id}`);
  }
  updateIdentity(id: string, rq: UpdateIdentityDtoRq): Promise<UpdateIdentityItemDtoRs> {
    return this.http.put<UpdateIdentityItemDtoRs>(`/api/swimmers/${id}/identity`, { body: rq });
  }
  createExam(id: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs> {
    return this.http.post<CreatedVitalsItemDtoRs>(`/api/swimmers/${id}/medical-exams`, { body: rq });
  }
  listExams(id: string): Promise<MedicalExamListDtoRs> {
    return this.http.get<MedicalExamListDtoRs>(`/api/swimmers/${id}/medical-exams`);
  }
  updateExam(id: string, examId: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs> {
    return this.http.put<CreatedVitalsItemDtoRs>(`/api/swimmers/${id}/medical-exams/${examId}`, { body: rq });
  }
  deleteExam(id: string, examId: string): Promise<DeleteExamItemDtoRs> {
    return this.http.delete<DeleteExamItemDtoRs>(`/api/swimmers/${id}/medical-exams/${examId}`);
  }
  getGuardians(id: string): Promise<GuardiansItemDtoRs> {
    return this.http.get<GuardiansItemDtoRs>(`/api/swimmers/${id}/guardians`);
  }
  upsertGuardians(id: string, rq: UpsertGuardiansDtoRq): Promise<UpsertGuardiansItemDtoRs> {
    return this.http.put<UpsertGuardiansItemDtoRs>(`/api/swimmers/${id}/guardians`, { body: rq });
  }
}
