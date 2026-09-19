import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateInBodyReadingDtoRq, isInBodyReadingDtoRsValid } from '@features/swimmer-profile/data/dto/inbody-reading.dto';
import { toInBodyReading } from '@features/swimmer-profile/data/dto/inbody-reading.mapper';
import { InBodyReading } from '@features/swimmer-profile/domain/model/inbody-reading';

export interface CreateInBodyReadingInput { id: string; rq: CreateInBodyReadingDtoRq; }

@Injectable({ providedIn: 'root' })
export class CreateInBodyReadingUseCase extends UseCase<CreateInBodyReadingInput, InBodyReading> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('CreateInBodyReading'); }
  protected async execute(input: CreateInBodyReadingInput): Promise<InBodyReading> {
    const res = await this.repo.createInBodyReading(input.id, input.rq);
    if (!isInBodyReadingDtoRsValid(res.data)) throw new AppError('Invalid InBody reading received', 'validation');
    return toInBodyReading(res.data);
  }
}
