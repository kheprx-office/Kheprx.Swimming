import { toChampionshipList } from '@features/championships/data/dto/competition-event.mapper';
import { isCompetitionEventListValid } from '@features/championships/data/dto/competition-event.dto';

describe('competition-event mapper', () => {
  const dto = [{
    id: 'e1', nameEn: 'National Junior Championship', nameAr: 'بطولة الناشئين',
    startDate: '2023-11-15', endDate: '2023-11-16',
    locationEn: 'Cairo Olympic Pool', locationAr: null,
    statusId: 'st1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: 'قادمة',
  }];

  it('accepts a valid dto list and maps every field', () => {
    expect(isCompetitionEventListValid(dto)).toBe(true);
    const list = toChampionshipList(dto);
    expect(list).toHaveLength(1);
    expect(list[0].nameEn).toBe('National Junior Championship');
    expect(list[0].endDate).toBe('2023-11-16');
    expect(list[0].statusCode).toBe('upcoming');
    expect(list[0].locationAr).toBeNull();
  });

  it('rejects a malformed dto list', () => {
    expect(isCompetitionEventListValid([{ id: 5 }])).toBe(false);
    expect(isCompetitionEventListValid('nope')).toBe(false);
  });
});
