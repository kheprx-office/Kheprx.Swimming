import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_PROFILE_REPOSITORY } from '@features/swimmer-profile/domain/repositories/swimmer-profile.repository';

export interface DeleteRecordInput { recordId: string; }

@Injectable({ providedIn: 'root' })
export class DeleteRecordUseCase extends UseCase<DeleteRecordInput, void> {
  private readonly repo = inject(SWIMMER_PROFILE_REPOSITORY);
  constructor() { super('DeleteRecord'); }
  protected async execute(input: DeleteRecordInput): Promise<void> {
    await this.repo.deleteRecord(input.recordId);
  }
}
