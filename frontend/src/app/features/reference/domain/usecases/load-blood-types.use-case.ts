// load-blood-types.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isCodedLookupListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadBloodTypesUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadBloodTypes'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getBloodTypes();
    if (!isCodedLookupListValid(res.data)) throw new AppError('Invalid blood types received', 'validation');
    return res.data.map((b) => ({ id: b.id, code: b.code, nameEn: b.nameEn, nameAr: b.nameAr }));
  }
}
