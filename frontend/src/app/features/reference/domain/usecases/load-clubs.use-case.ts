// load-clubs.use-case.ts
import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { AppError } from '@core/domain/errors/app-error';
import { REFERENCE_REPOSITORY } from '@features/reference/domain/repositories/reference.repository';
import { isClubListValid } from '@features/reference/data/dto/reference.dto';
import { LookupItem } from '@features/reference/domain/model/reference';

@Injectable({ providedIn: 'root' })
export class LoadClubsUseCase extends UseCase<void, LookupItem[]> {
  private readonly repo = inject(REFERENCE_REPOSITORY);
  constructor() { super('LoadClubs'); }
  protected async execute(): Promise<LookupItem[]> {
    const res = await this.repo.getClubs();
    if (!isClubListValid(res.data)) throw new AppError('Invalid clubs received', 'validation');
    return res.data.map((c) => ({ id: c.id, nameEn: c.nameEn, nameAr: c.nameAr }));
  }
}
