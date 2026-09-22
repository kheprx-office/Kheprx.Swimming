import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';
import { isFeedbackEntryListValid } from '@features/swimmer-profile/data/dto/feedback-entry.dto';
import { toFeedbackEntryList } from '@features/swimmer-profile/data/dto/feedback-entry.mapper';
import { FeedbackEntry } from '@features/swimmer-profile/domain/model/feedback-entry';

@Injectable({ providedIn: 'root' })
export class ListFeedbackEntriesUseCase extends UseCase<string, FeedbackEntry[]> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('ListFeedbackEntries'); }
  protected async execute(id: string): Promise<FeedbackEntry[]> {
    const res = await this.repo.getFeedbackEntries(id);
    if (!isFeedbackEntryListValid(res.data)) throw new AppError('Invalid feedback entries received', 'validation');
    return toFeedbackEntryList(res.data);
  }
}
