import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { isEnrollmentIdsValid } from '@features/championships/data/dto/enrollment.dto';

@Injectable({ providedIn: 'root' })
export class LoadEnrollmentsUseCase extends UseCase<string, string[]> {
  private readonly repo = inject(CHAMPIONSHIPS_REPOSITORY);
  constructor() { super('LoadEnrollments'); }

  protected async execute(eventId: string): Promise<string[]> {
    const res = await this.repo.getEnrollments(eventId);
    if (!isEnrollmentIdsValid(res.data)) throw new AppError('Invalid enrollments received', 'validation');
    return res.data;
  }
}
