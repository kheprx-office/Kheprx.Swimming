import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';
import { CreateMedicalTestDtoRq, isMedicalTestDtoRsValid } from '@features/medical-tests/data/dto/medical-test.dto';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';

@Injectable({ providedIn: 'root' })
export class CreateMedicalTestUseCase extends UseCase<CreateMedicalTestDtoRq, MedicalTest> {
  private readonly repo = inject(MEDICAL_TEST_REPOSITORY);
  constructor() { super('CreateMedicalTest'); }

  protected async execute(input: CreateMedicalTestDtoRq): Promise<MedicalTest> {
    const res = await this.repo.create(input);
    if (!isMedicalTestDtoRsValid(res.data)) throw new AppError('Invalid created medical test received', 'validation');
    const d = res.data;
    return {
      id: d.id, nameEn: d.nameEn, nameAr: d.nameAr, unit: d.unit,
      lowerBound: d.lowerBound, upperBound: d.upperBound, createdAt: d.createdAt,
    };
  }
}
