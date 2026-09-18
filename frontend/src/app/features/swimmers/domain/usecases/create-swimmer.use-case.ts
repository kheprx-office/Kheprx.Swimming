import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { CreateSwimmerDtoRq, isCreatedSwimmerDtoRsValid } from '@features/swimmers/data/dto/create-swimmer.dto';
import { CreatedSwimmer } from '@features/swimmers/domain/model/swimmer';

@Injectable({ providedIn: 'root' })
export class CreateSwimmerUseCase extends UseCase<CreateSwimmerDtoRq, CreatedSwimmer> {
  private readonly repo = inject(SWIMMER_REPOSITORY);
  constructor() { super('CreateSwimmer'); }
  protected async execute(input: CreateSwimmerDtoRq): Promise<CreatedSwimmer> {
    const res = await this.repo.create(input);
    if (!isCreatedSwimmerDtoRsValid(res.data)) throw new AppError('Invalid created swimmer received', 'validation');
    return { ...res.data };
  }
}
