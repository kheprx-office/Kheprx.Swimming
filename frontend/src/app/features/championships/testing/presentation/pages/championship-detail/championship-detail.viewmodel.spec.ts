import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ChampionshipDetailViewModel } from '@features/championships/presentation/pages/championship-detail/championship-detail.viewmodel';
import { LoadChampionshipUseCase } from '@features/championships/domain/usecases/load-championship.use-case';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { Championship } from '@features/championships/domain/model/championship';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { AppError } from '@core/domain/errors/app-error';

const champ: Championship = {
  id: 'e1', nameEn: 'National Junior', nameAr: 'الناشئين', startDate: '2023-11-15', endDate: '2023-11-16',
  locationEn: 'Cairo', locationAr: 'القاهرة', statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null,
};
const roster: SwimmerListItem[] = [
  { id: 's1', uid: 'U1', nameEn: 'Ahmed', nameAr: 'أحمد', clubNameEn: 'Oasis', clubNameAr: null, gender: 'male', age: 18 },
  { id: 's2', uid: 'U2', nameEn: 'Sara', nameAr: 'سارة', clubNameEn: 'Oasis', clubNameAr: null, gender: 'female', age: 16 },
  { id: 's3', uid: 'U3', nameEn: 'Omar', nameAr: 'عمر', clubNameEn: 'North', clubNameAr: null, gender: 'male', age: 15 },
];

interface Opts {
  role?: string;
  champRun?: jest.Mock;
  enrRun?: jest.Mock;
  saveRun?: jest.Mock;
  rosterRun?: jest.Mock;
}

function setup(opts: Opts = {}) {
  const champRun = opts.champRun ?? jest.fn().mockResolvedValue(ok(champ));
  const enrRun = opts.enrRun ?? jest.fn().mockResolvedValue(ok(['s1']));
  const saveRun = opts.saveRun ?? jest.fn().mockResolvedValue(ok(['s1']));
  const rosterRun = opts.rosterRun ?? jest.fn().mockResolvedValue(ok(roster));
  const notify = { success: jest.fn(), error: jest.fn() };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      ChampionshipDetailViewModel,
      { provide: LoadChampionshipUseCase, useValue: { run: champRun } },
      { provide: LoadEnrollmentsUseCase, useValue: { run: enrRun } },
      { provide: SaveEnrollmentsUseCase, useValue: { run: saveRun } },
      { provide: ListSwimmersUseCase, useValue: { run: rosterRun } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
      { provide: AuthSessionStore, useValue: { role: () => opts.role ?? 'head_coach' } },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: { t: (k: string) => k } },
    ],
  });
  return { vm: TestBed.inject(ChampionshipDetailViewModel), champRun, enrRun, saveRun, rosterRun, notify };
}

describe('ChampionshipDetailViewModel', () => {
  it('loads the event, roster and enrolled set', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.loading()).toBe(false);
    expect(vm.total()).toBe(3);
    expect(vm.enrolledCount()).toBe(1);
    expect(vm.isEnrolled('s1')).toBe(true);
    expect(vm.isEnrolled('s2')).toBe(false);
  });

  it('sets notFound on a 404', async () => {
    const champRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'http', 404) });
    const { vm } = setup({ champRun });
    await vm.load('e1');
    expect(vm.notFound()).toBe(true);
  });

  it('sets error on a non-404 failure', async () => {
    const champRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'network') });
    const { vm } = setup({ champRun });
    await vm.load('e1');
    expect(vm.error()).toBe(true);
  });

  it('toggle adds/removes and drives dirty + count', async () => {
    const { vm } = setup();
    await vm.load('e1');
    expect(vm.dirty()).toBe(false);
    vm.toggle('s2');
    expect(vm.isEnrolled('s2')).toBe(true);
    expect(vm.enrolledCount()).toBe(2);
    expect(vm.dirty()).toBe(true);
    vm.toggle('s2');
    expect(vm.dirty()).toBe(false);   // back to the baseline set
  });

  it('toggle is a no-op for a non-manager', async () => {
    const { vm } = setup({ role: 'swimmer' });
    await vm.load('e1');
    vm.toggle('s2');
    expect(vm.isEnrolled('s2')).toBe(false);
    expect(vm.dirty()).toBe(false);
  });

  it('filters the roster by name (EN + AR)', async () => {
    const { vm } = setup();
    await vm.load('e1');
    vm.setSearch('sara');
    expect(vm.filtered().map((s) => s.id)).toEqual(['s2']);
    vm.setSearch('عمر');
    expect(vm.filtered().map((s) => s.id)).toEqual(['s3']);
  });

  it('save persists the working set, toasts success and clears dirty', async () => {
    const { vm, saveRun, notify } = setup();
    await vm.load('e1');
    vm.toggle('s2');
    saveRun.mockResolvedValue(ok(['s1', 's2']));
    await vm.save();
    expect(saveRun).toHaveBeenCalledWith({ eventId: 'e1', swimmerIds: ['s1', 's2'] });
    expect(notify.success).toHaveBeenCalled();
    expect(vm.dirty()).toBe(false);
  });

  it('save surfaces an error toast and keeps the working set on failure', async () => {
    const saveRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'network') });
    const { vm, notify } = setup({ saveRun });
    await vm.load('e1');
    vm.toggle('s2');
    await vm.save();
    expect(notify.error).toHaveBeenCalled();
    expect(vm.isEnrolled('s2')).toBe(true);
    expect(vm.dirty()).toBe(true);
  });

  it('save is a no-op for a non-manager', async () => {
    const saveRun = jest.fn();
    const { vm } = setup({ role: 'swimmer', saveRun });
    await vm.load('e1');
    await vm.save();
    expect(saveRun).not.toHaveBeenCalled();
  });

  it('canManage reflects the role', () => {
    expect(setup({ role: 'captain' }).vm.canManage()).toBe(true);
    expect(setup({ role: 'swimmer' }).vm.canManage()).toBe(false);
  });

  it('enrollment load failure leaves the tab non-editable and Save a no-op', async () => {
    const enrRun = jest.fn().mockResolvedValue({ ok: false, error: new AppError('x', 'http', 500) });
    const saveRun = jest.fn();
    const { vm } = setup({ enrRun, saveRun });
    await vm.load('e1');
    expect(vm.enrollmentsLoaded()).toBe(false);
    expect(vm.error()).toBe(true);
    expect(vm.championship()).not.toBeNull();
    await vm.save();
    expect(saveRun).not.toHaveBeenCalled();
  });
});
