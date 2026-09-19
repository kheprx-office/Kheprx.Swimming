import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { UpdateIdentityDtoRq } from '@features/swimmer-profile/data/dto/update-identity.dto';

export interface UpdateSwimmerIdentityInput { id: string; rq: UpdateIdentityDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateSwimmerIdentityUseCase extends UseCase<UpdateSwimmerIdentityInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateSwimmerIdentity'); }

  protected async execute(input: UpdateSwimmerIdentityInput): Promise<void> {
    await this.repo.updateIdentity(input.id, input.rq);
  }
}
