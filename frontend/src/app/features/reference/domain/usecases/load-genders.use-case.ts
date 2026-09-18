// load-genders.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadGendersUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadGenders'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getGenders();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid genders received', 'validation');
    return res.data.map((g) => ({ id: g.id, code: g.code, nameEn: g.nameEn, nameAr: g.nameAr }));
  }
}
