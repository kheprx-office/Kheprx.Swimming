import { TestBed } from '@angular/core/testing';
import { LoadSwimmerChampionshipHistoryUseCase } from '@features/championships/domain/usecases/load-swimmer-championship-history.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';

const history = [{
  eventId: 'e1', nameEn: 'National', nameAr: null,
  startDate: '2023-11-15', endDate: '2023-11-16', locationEn: 'Cairo', locationAr: null,
  races: [{ dayLabelEn: 'Day 1', dayLabelAr: null, distanceId: 'd1', strokeId: 's1', timeMs: 52340, isPersonalBest: true }],
}];

function setup(getResult: unknown) {
  const repo = { getSwimmerChampionshipHistory: jest.fn().mockResolvedValue(getResult) };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: CHAMPIONSHIPS_REPOSITORY, useValue: repo }] });
  return { uc: TestBed.inject(LoadSwimmerChampionshipHistoryUseCase), repo };
}

describe('LoadSwimmerChampionshipHistoryUseCase', () => {
  it('loads and maps a valid response', async () => {
    const { uc, repo } = setup({ data: history });
    const res = await uc.run('sw1');
    expect(repo.getSwimmerChampionshipHistory).toHaveBeenCalledWith('sw1');
    expect(res.ok).toBe(true);
    if (res.ok) { expect(res.data[0].races[0].timeMs).toBe(52340); }
  });

  it('fails on an invalid payload', async () => {
    const { uc } = setup({ data: [{ eventId: 'e1' }] }); // missing required fields
    const res = await uc.run('sw1');
    expect(res.ok).toBe(false);
  });
});
