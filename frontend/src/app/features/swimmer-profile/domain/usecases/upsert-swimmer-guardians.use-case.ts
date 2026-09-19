import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { UpsertGuardiansDtoRq } from '@features/swimmer-profile/data/dto/guardians.dto';

export interface UpsertSwimmerGuardiansInput { id: string; rq: UpsertGuardiansDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpsertSwimmerGuardiansUseCase extends UseCase<UpsertSwimmerGuardiansInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpsertSwimmerGuardians'); }
  protected async execute(input: UpsertSwimmerGuardiansInput): Promise<void> {
    await this.repo.upsertGuardians(input.id, input.rq);
  }
}
