import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { MedicalTestDtoRs, isMedicalTestDtoRsValid } from '@features/medical-tests/data/dto/medical-test.dto';

// ListMedicalTestsUseCase: fetches the catalog (GET /api/medical-tests). Drops any row that
// fails validation, so a single malformed item never blanks the whole list.
@Injectable({ providedIn: 'root' })
export class ListMedicalTestsUseCase extends UseCase<void, MedicalTest[]> {
  private readonly repo = inject(MEDICAL_TEST_REPOSITORY);
  constructor() { super('ListMedicalTests'); }

  protected async execute(): Promise<MedicalTest[]> {
    const res = await this.repo.list();
    const items = Array.isArray(res.data) ? res.data : [];
    return items.filter(isMedicalTestDtoRsValid).map(toModel);
  }
}

function toModel(d: MedicalTestDtoRs): MedicalTest {
  return {
    id: d.id,
    nameEn: d.nameEn,
    nameAr: d.nameAr,
    unit: d.unit,
    lowerBound: d.lowerBound,
    upperBound: d.upperBound,
    createdAt: d.createdAt,
  };
}
