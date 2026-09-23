import { TestBed } from '@angular/core/testing';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { CompetitionEventListDtoRs } from '@features/championships/data/dto/competition-event.dto';

const validDto = {
  id: 'e1', nameEn: 'Nats', nameAr: null,
  startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo', locationAr: null,
  statusId: 'st1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: 'قادمة',
};

function make(getChampionships: () => Promise<CompetitionEventListDtoRs>) {
  TestBed.configureTestingModule({
    providers: [
      LoadChampionshipsUseCase,
      { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { getChampionships } },
    ],
  });
  return TestBed.inject(LoadChampionshipsUseCase);
}

describe('LoadChampionshipsUseCase', () => {
  it('maps a valid list', async () => {
    const uc = make(async () => ({ data: [validDto] } as unknown as CompetitionEventListDtoRs));
    const res = await uc.run();
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data[0].nameEn).toBe('Nats');
  });

  it('fails on an invalid payload', async () => {
    const uc = make(async () => ({ data: [{ id: 5 }] } as unknown as CompetitionEventListDtoRs));
    const res = await uc.run();
    expect(res.ok).toBe(false);
  });
});
