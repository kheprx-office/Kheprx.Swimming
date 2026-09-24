// load-distances.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadDistancesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadDistances'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getDistances();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid distances received', 'validation');
    return res.data.map((d) => ({ id: d.id, code: d.code, nameEn: d.nameEn, nameAr: d.nameAr }));
  }
}
