import { InjectionToken } from '@angular/core';
import { SwimmerProfileItemDtoRs, MedicalExamListDtoRs, DeleteExamItemDtoRs } from '@features/swimmer-profile/data/dto/swimmer-profile.dto';
import { UpdateIdentityDtoRq, UpdateIdentityItemDtoRs } from '@features/swimmer-profile/data/dto/update-identity.dto';
import { CreateMedicalExamDtoRq, CreatedVitalsItemDtoRs } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';

export interface ISwimmerProfileRepository {
  getProfile(id: string): Promise<SwimmerProfileItemDtoRs>;
  updateIdentity(id: string, rq: UpdateIdentityDtoRq): Promise<UpdateIdentityItemDtoRs>;
  createExam(id: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs>;
  listExams(id: string): Promise<MedicalExamListDtoRs>;
  updateExam(id: string, examId: string, rq: CreateMedicalExamDtoRq): Promise<CreatedVitalsItemDtoRs>;
  deleteExam(id: string, examId: string): Promise<DeleteExamItemDtoRs>;
}

export const SWIMMER_PROFILE_REPOSITORY = new InjectionToken<ISwimmerProfileRepository>('SWIMMER_PROFILE_REPOSITORY');
