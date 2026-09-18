// medical-test.repository.impl.ts — medical-tests repository. Returns the API DTO envelope.
import { Injectable, inject } from '@angular/core';
import { HttpClientService } from '@core/network/api/http-client';
import { IMedicalTestRepository } from '@features/medical-tests/domain/repositories/medical-test.repository';
import {
  MedicalTestListDtoRs,
  MedicalTestItemDtoRs,
  MedicalTestDeletedDtoRs,
  CreateMedicalTestDtoRq,
} from '@features/medical-tests/data/dto/medical-test.dto';

@Injectable({ providedIn: 'root' })
export class MedicalTestRepositoryImpl implements IMedicalTestRepository {
  private readonly http = inject(HttpClientService);

  list(): Promise<MedicalTestListDtoRs> {
    return this.http.get<MedicalTestListDtoRs>('/api/medical-tests');
  }

  create(rq: CreateMedicalTestDtoRq): Promise<MedicalTestItemDtoRs> {
    return this.http.post<MedicalTestItemDtoRs>('/api/medical-tests', { body: rq });
  }

  delete(id: string): Promise<MedicalTestDeletedDtoRs> {
    return this.http.delete<MedicalTestDeletedDtoRs>(`/api/medical-tests/${id}`);
  }
}
