import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { CreateFeedbackEntryDtoRq, isFeedbackEntryDtoRsValid } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { toFeedbackEntry } from '@features/swimmer-profile/data/dto/feedback-entry.mapper';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';

export interface UpdateFeedbackEntryInput { id: string; entryId: string; rq: CreateFeedbackEntryDtoRq; }

@Injectable({ providedIn: 'root' })
export class UpdateFeedbackEntryUseCase extends UseCase<UpdateFeedbackEntryInput, FeedbackEntry> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('UpdateFeedbackEntry'); }
  protected async execute(input: UpdateFeedbackEntryInput): Promise<FeedbackEntry> {
    const res = await this.repo.updateFeedbackEntry(input.id, input.entryId, input.rq);
    if (!isFeedbackEntryDtoRsValid(res.data)) throw new AppError('Invalid feedback entry received', 'validation');
    return toFeedbackEntry(res.data);
  }
}
