import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ChampionshipDetailPage } from '@features/championships/presentation/pages/championship-detail/championship-detail.page';
import { ChampionshipDetailViewModel } from '@features/championships/presentation/pages/championship-detail/championship-detail.viewmodel';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { Championship } from '@features/championships/domain/model/championship';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';

const champ: Championship = {
  id: 'e1', nameEn: 'National Junior Championship', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo Olympic Pool', locationAr: null, statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null,
};
const roster: SwimmerListItem[] = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed Al-Rashidi', nameAr: null, clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
];

describe('ChampionshipDetailPage', () => {
  it('renders the event header, the enabled + disabled tabs and the roster', async () => {
    TestBed.configureTestingModule({
      imports: [ChampionshipDetailPage],
      providers: [
        ChampionshipDetailViewModel,
        { provide: LoadChampionshipUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(champ)) } },
        { provide: LoadEnrollmentsUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(['s1'])) } },
        { provide: SaveEnrollmentsUseCase, useValue: { run: jest.fn() } },
        { provide: ListSwimmersUseCase, useValue: { run: jest.fn().mockResolvedValue(ok(roster)) } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
        { provide: AuthSessionStore, useValue: { role: () => 'head_coach' } },
        { provide: NotificationService, useValue: { success: jest.fn(), error: jest.fn() } },
        { provide: TranslateService, useValue: { t: (k: string) => k } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'e1' } } } },
      ],
    });
    const fixture = TestBed.createComponent(ChampionshipDetailPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('National Junior Championship');
    expect(text).toContain('Ahmed Al-Rashidi');

    // The three unbuilt tabs are disabled; Enrollment is not.
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const disabledLabels = buttons.filter((b) => b.disabled).map((b) => b.textContent?.trim());
    expect(disabledLabels).toEqual(
      expect.arrayContaining([
        'championships.detail.tabs.days',
        'championships.detail.tabs.finished',
        'championships.detail.tabs.results',
      ]),
    );
  });
});
