// load-observation-categories.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadObservationCategoriesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadObservationCategories'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getObservationCategories();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid observation categories received', 'validation');
    return res.data.map((c) => ({ id: c.id, code: c.code, nameEn: c.nameEn, nameAr: c.nameAr }));
  }
}
