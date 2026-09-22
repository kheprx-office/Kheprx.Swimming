import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteFeedbackEntryInput { id: string; entryId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteFeedbackEntryUseCase extends UseCase<DeleteFeedbackEntryInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteFeedbackEntry'); }
  protected async execute(input: DeleteFeedbackEntryInput): Promise<void> {
    await this.repo.deleteFeedbackEntry(input.id, input.entryId);
  }
}
