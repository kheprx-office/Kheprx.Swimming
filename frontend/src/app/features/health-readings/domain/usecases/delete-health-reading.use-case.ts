import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { HEALTH_READING_REPOSITORY } from '@features/health-readings/domain/repositories/health-reading.repository';

export interface DeleteHealthReadingInput { id: string; }

@Injectable({ providedIn: 'root' })
export class DeleteHealthReadingUseCase extends UseCase<DeleteHealthReadingInput, void> {
  private readonly repo = inject(HEALTH_READING_REPOSITORY);
  constructor() { super('DeleteHealthReading'); }
  protected async execute(input: DeleteHealthReadingInput): Promise<void> {
    await this.repo.remove(input.id);
  }
}
