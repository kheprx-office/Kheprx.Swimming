import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateMedicalExamDtoRq } from '@features/swimmer-profile/data/dto/create-medical-exam.dto';
import { toVitals } from '@features/swimmer-profile/data/dto/vitals.mapper';
import { SwimmerVitals } from '@features/swimmer-profile/domain/model/swimmer-profile';

export interface CreateMedicalExamInput { id: string; rq: CreateMedicalExamDtoRq; }

@Injectable({ providedIn: 'root' })
export class CreateMedicalExamUseCase extends UseCase<CreateMedicalExamInput, SwimmerVitals> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('CreateMedicalExam'); }

  protected async execute(input: CreateMedicalExamInput): Promise<SwimmerVitals> {
    const res = await this.repo.createExam(input.id, input.rq);
    const d = res.data;
    if (!d || typeof d.hemoglobin !== 'number') throw new AppError('Invalid created exam received', 'validation');
    return toVitals(d);
  }
}
