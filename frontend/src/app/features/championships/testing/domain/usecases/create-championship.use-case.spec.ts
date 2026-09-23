import { TestBed } from '@angular/core/testing';
import { CreateChampionshipUseCase } from '@features/championships/domain/usecases/create-championship.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';
import { CompetitionEventItemDtoRs, CreateChampionshipRq } from '@features/championships/data/dto/competition-event.dto';

const validDto = {
  id: 'e1', nameEn: 'Summer Cup', nameAr: null,
  startDate: '2024-06-01', endDate: '2024-06-03',
  locationEn: 'Cairo', locationAr: null,
  statusId: 'st1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: 'قادمة',
};

const rq: CreateChampionshipRq = { name: 'Summer Cup', startDate: '2024-06-01', endDate: '2024-06-03', location: 'Cairo' };

function make(createChampionship: (r: CreateChampionshipRq) => Promise<CompetitionEventItemDtoRs>) {
  TestBed.configureTestingModule({
    providers: [
      CreateChampionshipUseCase,
      { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { createChampionship } },
    ],
  });
  return TestBed.inject(CreateChampionshipUseCase);
}

describe('CreateChampionshipUseCase', () => {
  it('posts the request and maps the created championship', async () => {
    let received: CreateChampionshipRq | null = null;
    const uc = make(async (r) => { received = r; return { data: validDto } as unknown as CompetitionEventItemDtoRs; });
    const res = await uc.run(rq);
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.nameEn).toBe('Summer Cup');
    expect(received).toEqual(rq);
  });

  it('fails on an invalid payload', async () => {
    const uc = make(async () => ({ data: { id: 5 } } as unknown as CompetitionEventItemDtoRs));
    const res = await uc.run(rq);
    expect(res.ok).toBe(false);
  });
});
