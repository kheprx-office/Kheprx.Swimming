import { InjectionToken } from '@angular/core';
import {
  MedicalTestListDtoRs,
  MedicalTestItemDtoRs,
  MedicalTestDeletedDtoRs,
  CreateMedicalTestDtoRq,
} from '@features/medical-tests/data/dto/medical-test.dto';

export interface IMedicalTestRepository {
  list(): Promise<MedicalTestListDtoRs>;
  create(rq: CreateMedicalTestDtoRq): Promise<MedicalTestItemDtoRs>;
  delete(id: string): Promise<MedicalTestDeletedDtoRs>;
}

export const MEDICAL_TEST_REPOSITORY = new InjectionToken<IMedicalTestRepository>('MEDICAL_TEST_REPOSITORY');
