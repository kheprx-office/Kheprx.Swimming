import { Injectable, inject } from '@angular/core';
import { UseCase } from '@core/domain/usecase/use-case';
import { SWIMMER_REPOSITORY } from '@features/swimmers/domain/repositories/swimmer.repository';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { SwimmerListItemDtoRs, isSwimmerListItemDtoRsValid } from '@features/swimmers/data/dto/swimmer-list.dto';

// ListSwimmersUseCase: fetches the roster (GET /api/swimmers), optionally filtered by a
// server-side search term. Drops any row that fails validation, so a single malformed
// item never blanks the whole list.
@Injectable({ providedIn: 'root' })
export class ListSwimmersUseCase extends UseCase<string | undefined, SwimmerListItem[]> {
  private readonly repo = inject(SWIMMER_REPOSITORY);
  constructor() { super('ListSwimmers'); }

  protected async execute(search?: string): Promise<SwimmerListItem[]> {
    const res = await this.repo.list(search);
    const items = Array.isArray(res.data) ? res.data : [];
    return items.filter(isSwimmerListItemDtoRsValid).map(toModel);
  }
}

function toModel(d: SwimmerListItemDtoRs): SwimmerListItem {
  return {
    id: d.id,
    uid: d.uid,
    nameEn: d.nameEn,
    nameAr: d.nameAr ?? null,
    clubNameEn: d.clubNameEn ?? null,
    clubNameAr: d.clubNameAr ?? null,
    gender: d.genderCode === 'male' || d.genderCode === 'female' ? d.genderCode : null,
    age: typeof d.age === 'number' ? d.age : null,
  };
}
