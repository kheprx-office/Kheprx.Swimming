import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteInBodyReadingInput { id: string; readingId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteInBodyReadingUseCase extends UseCase<DeleteInBodyReadingInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteInBodyReading'); }
  protected async execute(input: DeleteInBodyReadingInput): Promise<void> {
    await this.repo.deleteInBodyReading(input.id, input.readingId);
  }
}
