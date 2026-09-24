import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ChampionshipDetailPage } from '@features/championships/presentation/pages/championship-detail/championship-detail.page';
import { ChampionshipDetailViewModel } from '@features/championships/presentation/pages/championship-detail/championship-detail.viewmodel';
import { CompetitionDaysViewModel } from '@features/championships/presentation/pages/championship-detail/competition-days.viewmodel';
import { RaceResultsViewModel } from '@features/championships/presentation/pages/championship-detail/race-results.viewmodel';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { Championship } from '@features/championships/domain/model/championship';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';

const champ: Championship = {
  id: 'e1', nameEn: 'National Junior Championship', nameAr: null,
  startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo', locationAr: null,
  statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null,
};
const roster: SwimmerListItem[] = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed', nameAr: null, clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
];

function setup() {
  const detailVm = {
    load: jest.fn(),
    setTab: jest.fn(),
    activeTab: () => 'enrollment',
    loading: () => true, error: () => false, notFound: () => false, championship: () => champ,
  };
  const daysVm = { ensureLoaded: jest.fn() };
  const resultsVm = { ensureLoaded: jest.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [ChampionshipDetailPage],
    providers: [
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'e1' } } } },
    ],
  })
    .overrideComponent(ChampionshipDetailPage, {
      set: { providers: [
        { provide: ChampionshipDetailViewModel, useValue: detailVm },
        { provide: CompetitionDaysViewModel, useValue: daysVm },
        { provide: RaceResultsViewModel, useValue: resultsVm },
      ] },
    });
  const fixture = TestBed.createComponent(ChampionshipDetailPage);
  return { c: fixture.componentInstance, detailVm, daysVm, resultsVm };
}

describe('ChampionshipDetailPage', () => {
  it('enables all four detail tabs', () => {
    const { c } = setup();
    expect(c.isEnabled('enrollment')).toBe(true);
    expect(c.isEnabled('days')).toBe(true);
    expect(c.isEnabled('finished')).toBe(true);
    expect(c.isEnabled('results')).toBe(true);
  });

  it('lazy-loads the days schedule on first activation of the days tab', () => {
    const { c, detailVm, daysVm } = setup();
    c.ngOnInit();
    c.onTab('days');
    expect(detailVm.setTab).toHaveBeenCalledWith('days');
    expect(daysVm.ensureLoaded).toHaveBeenCalledWith('e1', '2023-11-15', '2023-11-16');
  });

  it('lazy-loads results on first activation of the finished tab', () => {
    const { c, detailVm, resultsVm } = setup();
    c.ngOnInit();
    c.onTab('finished');
    expect(detailVm.setTab).toHaveBeenCalledWith('finished');
    expect(resultsVm.ensureLoaded).toHaveBeenCalledWith('e1');
  });

  it('renders the header, enables the days tab, and renders the days section on activation', async () => {
    const daysMock = {
      loading: () => false, error: () => false, loaded: () => true, saving: () => false,
      canManage: () => true, dirty: () => false, canAddDay: () => true,
      dayCount: () => 1, raceCount: () => 1, entryCount: () => 1,
      strokes: () => [{ id: 'st1', code: 'freestyle', nameEn: 'Freestyle', nameAr: null }],
      distances: () => [{ id: 'ds1', code: '50m', nameEn: '50m', nameAr: null }],
      enrolledSwimmers: () => roster,
      days: () => [{ key: 'k1', labelEn: 'Day 1', labelAr: null, dayDate: '2023-11-15',
        races: [{ key: 'k2', strokeId: 'st1', distanceId: 'ds1', scheduledTime: '09:00', swimmerIds: [] }] }],
      ensureLoaded: jest.fn(), addDay: jest.fn(), removeDay: jest.fn(), updateDay: jest.fn(),
      addRace: jest.fn(), removeRace: jest.fn(), updateRace: jest.fn(), toggleSwimmer: jest.fn(), save: jest.fn(),
      isAssigned: () => false, statusOf: () => 'scheduled', swimmerName: (s: SwimmerListItem) => s.nameEn,
      strokeLabel: (_: string) => 'Freestyle', distanceLabel: (_: string) => '50m',
      dayDateLabel: (iso: string) => (iso ? '15 Nov 2023' : ''),
    };
    TestBed.resetTestingModule();
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
    }).overrideComponent(ChampionshipDetailPage, { set: { providers: [
      { provide: CompetitionDaysViewModel, useValue: daysMock },
      { provide: RaceResultsViewModel, useValue: { loading: () => false, loaded: () => false, error: () => false, finishedRaces: () => [], resultRaces: () => [], finishedCount: () => 0, canManage: () => true, ensureLoaded: jest.fn(), isOpen: () => false, timeInput: () => '', openResults: jest.fn(), setTime: jest.fn(), saveResults: jest.fn(), formatTime: (n: number) => String(n) } },
    ] } });

    const fixture = TestBed.createComponent(ChampionshipDetailPage);
    fixture.detectChanges(); await fixture.whenStable();
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();

    const text = () => fixture.nativeElement.textContent as string;
    expect(text()).toContain('National Junior Championship');

    const buttons = () => Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    const daysBtn = buttons().find((b) => b.textContent?.includes('championships.detail.tabs.days'))!;
    expect(daysBtn.disabled).toBe(false);

    fixture.componentInstance.onTab('days');
    fixture.detectChanges();
    expect(daysMock.ensureLoaded).toHaveBeenCalledWith('e1', '2023-11-15', '2023-11-16');
    // day label lives in an <input [ngModel]>; confirm the day card rendered by checking
    // that the text input for the label is present in the DOM (one per day)
    const dayLabelInputs = Array.from(fixture.nativeElement.querySelectorAll('input[type="text"]')) as HTMLInputElement[];
    expect(dayLabelInputs.length).toBeGreaterThanOrEqual(1); // day card's label input rendered
    expect(text()).toContain('championships.days.save');     // save button rendered
    // the day date is a READ-ONLY label now, not an editable date input
    expect(fixture.nativeElement.querySelectorAll('input[type="date"]').length).toBe(0);
    expect(text()).toContain('15 Nov 2023');                 // formatted date label rendered
  });
});
