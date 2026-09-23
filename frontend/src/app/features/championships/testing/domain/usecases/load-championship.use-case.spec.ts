import { TestBed } from '@angular/core/testing';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';

const validDto = {
  id: 'e1', nameEn: 'Nats', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo', locationAr: null, statusId: 'st1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null,
};

function make(getChampionship: (id: string) => Promise<unknown>) {
  TestBed.configureTestingModule({
    providers: [
      LoadChampionshipUseCase,
      { provide: CHAMPIONSHIPS_REPOSITORY, useValue: { getChampionship } },
    ],
  });
  return TestBed.inject(LoadChampionshipUseCase);
}

describe('LoadChampionshipUseCase', () => {
  it('maps a valid event', async () => {
    const uc = make(async () => ({ data: validDto }));
    const res = await uc.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data.nameEn).toBe('Nats');
  });

  it('fails on an invalid payload', async () => {
    const uc = make(async () => ({ data: { id: 5 } }));
    const res = await uc.run('e1');
    expect(res.ok).toBe(false);
  });
});
