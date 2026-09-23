import { TestBed } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ChampionshipsPage } from '@features/championships/presentation/pages/championships/championships.page';
import { ChampionshipsViewModel } from '@features/championships/presentation/pages/championships/championships.viewmodel';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { CreateChampionshipUseCase } from '@features/championships/domain/usecases/create-championship.use-case';
import { Championship } from '@features/championships/domain/model/championship';

const sample: Championship[] = [
  { id: '1', nameEn: 'National Junior Championship', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
    locationEn: 'Cairo Olympic Pool', locationAr: null, statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null },
];

describe('ChampionshipsPage', () => {
  it('renders championship rows after load', async () => {
    const run = jest.fn().mockResolvedValue(ok(sample));
    TestBed.configureTestingModule({
      imports: [ChampionshipsPage, RouterModule.forRoot([])],
      providers: [
        ChampionshipsViewModel,
        { provide: LoadChampionshipsUseCase, useValue: { run } },
        { provide: CreateChampionshipUseCase, useValue: { run: jest.fn() } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
        { provide: AuthSessionStore, useValue: { role: () => 'head_coach' } },
        { provide: NotificationService, useValue: { success: jest.fn(), error: jest.fn() } },
        { provide: TranslateService, useValue: { t: (k: string) => k } },
      ],
    });
    const fixture = TestBed.createComponent(ChampionshipsPage);
    fixture.detectChanges();          // ngOnInit -> load()
    await fixture.whenStable();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('National Junior Championship');
    expect(text).toContain('Cairo Olympic Pool');
  });

  it('links each row to its detail route', async () => {
    const run = jest.fn().mockResolvedValue(ok(sample));
    TestBed.configureTestingModule({
      imports: [ChampionshipsPage, RouterModule.forRoot([])],
      providers: [
        ChampionshipsViewModel,
        { provide: LoadChampionshipsUseCase, useValue: { run } },
        { provide: CreateChampionshipUseCase, useValue: { run: jest.fn() } },
        { provide: LanguageStore, useValue: { lang: () => 'en' } },
        { provide: AuthSessionStore, useValue: { role: () => 'head_coach' } },
        { provide: NotificationService, useValue: { success: jest.fn(), error: jest.fn() } },
        { provide: TranslateService, useValue: { t: (k: string) => k } },
      ],
    });
    const fixture = TestBed.createComponent(ChampionshipsPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const link = fixture.nativeElement.querySelector('a[href="/championships/1"]');
    expect(link).toBeTruthy();
  });
});
