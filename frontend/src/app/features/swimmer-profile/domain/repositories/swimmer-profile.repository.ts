import { InjectionToken } from '@angular/core';
import { SwimmerProfileItemDtoRs, MedicalExamListDtoRs, DeleteExamItemDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { UpdateIdentityDtoRq, UpdateIdentityItemDtoRs } from '@features/swimmer-profile/data/dto/update-identity.dto';
import { CreateMedicalExamDtoRq, CreatedVitalsItemDtoRs } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';
import { GuardiansItemDtoRs, UpsertGuardiansDtoRq, UpsertGuardiansItemDtoRs } from '@features/swimmer-profile/data/dto/guardians.dto';
import { BodyMeasurementItemDtoRs, CreateBodyMeasurementDtoRq, CreateBodyMeasurementItemDtoRs } from '@features/swimmer-profile/data/dto/body-measurement.dto';
import { InBodyReadingListDtoRs, InBodyReadingItemDtoRs, DeleteInBodyReadingItemDtoRs, CreateInBodyReadingDtoRq } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { RecordListDtoRs, RecordItemDtoRs, DeleteRecordItemDtoRs, UpdateRecordDtoRq } from '@features/swimmer-profile/data/dto/record.dto';
import { FeedbackEntryListDtoRs, FeedbackEntryItemDtoRs, DeleteFeedbackEntryItemDtoRs, CreateFeedbackEntryDtoRq } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { AttendanceRecordListDtoRs } from '@features/swimmer-profile/data/dto/attendance-record.dto';
import { MySwimmerRefItemDtoRs } from '@features/swimmer-profile/data/dto/my-swimmer-ref.dto';

export interface ISwimmerProfileRepository {
  getProfile(id: string): Promise<SwimmerProfileItemDtoRs>;
  updateIdentity(id: string, rq: UpdateIdentityDtoRq): Promise<UpdateIdentityItemDtoRs>;
  createExam(id: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs>;
  listExams(id: string): Promise<MedicalExamListDtoRs>;
  updateExam(id: string, examId: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs>;
  deleteExam(id: string, examId: string): Promise<DeleteExamItemDtoRs>;
  getGuardians(id: string): Promise<GuardiansItemDtoRs>;
  upsertGuardians(id: string, rq: UpsertGuardiansDtoRq): Promise<UpsertGuardiansItemDtoRs>;
  getBodyMeasurement(id: string): Promise<BodyMeasurementItemDtoRs>;
  createBodyMeasurement(id: string, rq: CreateBodyMeasurementDtoRq): Promise<CreateBodyMeasurementItemDtoRs>;
  getInBodyReadings(id: string): Promise<InBodyReadingListDtoRs>;
  createInBodyReading(id: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs>;
  updateInBodyReading(id: string, readingId: string, rq: CreateInBodyReadingDtoRq): Promise<InBodyReadingItemDtoRs>;
  deleteInBodyReading(id: string, readingId: string): Promise<DeleteInBodyReadingItemDtoRs>;
  listRecords(id: string): Promise<RecordListDtoRs>;
  updateRecord(recordId: string, rq: UpdateRecordDtoRq): Promise<RecordItemDtoRs>;
  deleteRecord(recordId: string): Promise<DeleteRecordItemDtoRs>;
  getFeedbackEntries(id: string): Promise<FeedbackEntryListDtoRs>;
  createFeedbackEntry(id: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs>;
  updateFeedbackEntry(id: string, entryId: string, rq: CreateFeedbackEntryDtoRq): Promise<FeedbackEntryItemDtoRs>;
  deleteFeedbackEntry(id: string, entryId: string): Promise<DeleteFeedbackEntryItemDtoRs>;
  getAttendanceRecords(id: string): Promise<AttendanceRecordListDtoRs>;
  getMySwimmerId(): Promise<MySwimmerRefItemDtoRs>;
}

export const SWIMMER_PROFILE_REPOSITORY = new InjectionToken<ISwimmerProfileRepository>('SWIMMER_PROFILE_REPOSITORY');
