import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteMedicalExamInput { id: string; examId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteMedicalExamUseCase extends UseCase<DeleteMedicalExamInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteMedicalExam'); }
  protected async execute(input: DeleteMedicalExamInput): Promise<void> {
    await this.repo.deleteExam(input.id, input.examId);
  }
}
