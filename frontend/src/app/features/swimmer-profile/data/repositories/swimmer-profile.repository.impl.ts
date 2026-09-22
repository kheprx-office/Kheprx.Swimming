// swimmer-profile.repository.impl.ts — profile read + identity/vitals writes.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { ISwimmerProfileRepository } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { SwimmerProfileItemDtoRs, MedicalExamListDtoRs, DeleteExamItemDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { UpdateIdentityDtoRq, UpdateIdentityItemDtoRs } from '@features/swimmer-profile/data/dto/update-identity.dto';
import { CreateMedicalExamDtoRq, CreatedVitalsItemDtoRs } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';
import { GuardiansItemDtoRs, UpsertGuardiansDtoRq, UpsertGuardiansItemDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';
import { BodyMeasurementItemDtoRs, CreateBodyMeasurementDtoRq, CreateBodyMeasurementItemDtoRs } from '@features/swimmer-profile/data/dto/body-measurement.dto';
import { InBodyReadingListDtoRs, InBodyReadingItemDtoRs, DeleteInBodyReadingItemDtoRs, CreateInBodyReadingDtoRq } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { RecordListDtoRs, RecordItemDtoRs, DeleteRecordItemDtoRs, UpdateRecordDtoRq } from '@features/swimmer-profile/data/dto/record.dto';
import { FeedbackEntryListDtoRs, FeedbackEntryItemDtoRs, DeleteFeedbackEntryItemDtoRs, CreateFeedbackEntryDtoRq } from '@features/swimmer-profile/data/dto/feedback-entry.dto';

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
  getBodyMeasurement(id: string): Promise<BodyMeasurementItemDtoRs> {
    return this.http.get<BodyMeasurementItemDtoRs>(`/api/swimmers/${id}/body-measurements/latest`);
  }
  createBodyMeasurement(id: string, rq: CreateBodyMeasurementDtoRq): Promise<CreateBodyMeasurementItemDtoRs> {
    return this.http.post<CreateBodyMeasurementItemDtoRs>(`/api/swimmers/${id}/body-measurements`, { body: rq });
  }
  getInBodyReadings(id: string): Promise<InBodyReadingListDtoRs> {
    return this.http.get<InBodyReadingListDtoRs>(`/api/swimmers/${id}/inbody-readings`);
  }
  createInBodyReading(id: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs> {
    return this.http.post<InBodyReadingItemDtoRs>(`/api/swimmers/${id}/inbody-readings`, { body: rq });
  }
  updateInBodyReading(id: string, readingId: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs> {
    return this.http.put<InBodyReadingItemDtoRs>(`/api/swimmers/${id}/inbody-readings/${readingId}`, { body: rq });
  }
  deleteInBodyReading(id: string, readingId: string): Promise<DeleteInBodyReadingItemDtoRs> {
    return this.http.delete<DeleteInBodyReadingItemDtoRs>(`/api/swimmers/${id}/inbody-readings/${readingId}`);
  }
  listRecords(id: string): Promise<RecordListDtoRs> {
    return this.http.get<RecordListDtoRs>(`/api/observations?swimmerId=${id}`);
  }
  updateRecord(recordId: string, rq: UpdateRecordDtoRq): Promise<RecordItemDtoRs> {
    return this.http.put<RecordItemDtoRs>(`/api/observations/${recordId}`, { body: rq });
  }
  deleteRecord(recordId: string): Promise<DeleteRecordItemDtoRs> {
    return this.http.delete<DeleteRecordItemDtoRs>(`/api/observations/${recordId}`);
  }
  getFeedbackEntries(id: string): Promise<FeedbackEntryListDtoRs> {
    return this.http.get<FeedbackEntryListDtoRs>(`/api/swimmers/${id}/feedback-entries`);
  }
  createFeedbackEntry(id: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs> {
    return this.http.post<FeedbackEntryItemDtoRs>(`/api/swimmers/${id}/feedback-entries`, { body: rq });
  }
  updateFeedbackEntry(id: string, entryId: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs> {
    return this.http.put<FeedbackEntryItemDtoRs>(`/api/swimmers/${id}/feedback-entries/${entryId}`, { body: rq });
  }
  deleteFeedbackEntry(id: string, entryId: string): Promise<DeleteFeedbackEntryItemDtoRs> {
    return this.http.delete<DeleteFeedbackEntryItemDtoRs>(`/api/swimmers/${id}/feedback-entries/${entryId}`);
  }
}
