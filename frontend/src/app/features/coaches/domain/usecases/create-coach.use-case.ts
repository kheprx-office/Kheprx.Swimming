import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { COACH_REPOSITORY } from '@features/coaches/domain/repositories/coach.repository';
import { CreateCoachDtoRq, isCreatedCoachDtoRsValid } from '@features/coaches/data/dto/create-coach.dto';
import { CreatedCoach } from '@features/coaches/domain/model/coach';

@Injectable({ providedIn: 'root' })
export class CreateCoachUseCase extends UseCase<CreateCoachDtoRq, CreatedCoach> {
  private readonly repo = inject(COACH_REPOSITORY);
  constructor() { super('CreateCoach'); }
  protected async execute(input: CreateCoachDtoRq): Promise<CreatedCoach> {
    const res = await this.repo.create(input);
    if (!isCreatedCoachDtoRsValid(res.data)) throw new AppError('Invalid created coach received', 'validation');
    return { ...res.data };
  }
}
