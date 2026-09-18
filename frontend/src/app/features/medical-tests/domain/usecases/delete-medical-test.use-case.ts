import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { MEDICAL_TEST_REPOSITORY } from '@features/medical-tests/domain/repositories/medical-test.repository';

// DeleteMedicalTestUseCase: DELETE /api/medical-tests/{id}. A 404 surfaces as an AppError
// with status 404 (the repository/http layer maps it), which the view model handles.
@Injectable({ providedIn: 'root' })
export class DeleteMedicalTestUseCase extends UseCase<string, void> {
  private readonly repo = inject(MEDICAL_TEST_REPOSITORY);
  constructor() { super('DeleteMedicalTest'); }

  protected async execute(id: string): Promise<void> {
    await this.repo.delete(id);
  }
}
