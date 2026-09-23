import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';

export interface SaveEnrollmentsInput {
  eventId: string;
  swimmerIds: string[];
}

@Injectable({ providedIn: 'root' })
export class SaveEnrollmentsUseCase extends UseCase<SaveEnrollmentsInput, string[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('SaveEnrollments'); }

  // The PUT replaces the whole set and returns ApiResponse<object> (data: null) — it does NOT echo the ids.
  // A resolved call is success, so the submitted set becomes the new authoritative baseline. A failed HTTP
  // call (4xx/5xx/network) throws, which run() converts to Result.fail → the caller shows the error toast.
  protected async execute(input: SaveEnrollmentsInput): Promise<string[]> {
    await this.repo.setEnrollments(input.eventId, { swimmerIds: input.swimmerIds });
    return input.swimmerIds;
  }
}
