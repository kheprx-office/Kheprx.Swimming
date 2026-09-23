import { TestBed } from '@angular/core/testing';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';
import { TranslateService } from '@core/i18n';
import { NotificationService } from '@core/ui/notification.service';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { ChampionshipsViewModel } from '@features/championships/presentation/pages/championships/championships.viewmodel';
import { LoadChampionshipsUseCase } from '@features/championships/domain/usecases/load-championships.use-case';
import { CreateChampionshipUseCase } from '@features/championships/domain/usecases/create-championship.use-case';
import { Championship } from '@features/championships/domain/model/championship';

const sample: Championship[] = [
  { id: '1', nameEn: 'Nov Meet', nameAr: null, startDate: '2023-11-15', endDate: '2023-11-16',
    locationEn: 'Cairo', locationAr: null, statusId: 's1', statusCode: 'upcoming', statusNameEn: 'Upcoming', statusNameAr: null },
  { id: '2', nameEn: 'Oct Meet', nameAr: null, startDate: '2023-10-20', endDate: '2023-10-20',
    locationEn: 'Alex', locationAr: null, statusId: 's2', statusCode: 'completed', statusNameEn: 'Completed', statusNameAr: null },
];

interface SetupOpts {
  role?: string;
  loadRun?: jest.Mock;
  createRun?: jest.Mock;
}

function setup(opts: SetupOpts = {}) {
  const run = opts.loadRun ?? jest.fn().mockResolvedValue(ok(sample));
  const createRun = opts.createRun ?? jest.fn().mockResolvedValue(ok(sample[0]));
  const notify = { success: jest.fn(), error: jest.fn() };
  TestBed.configureTestingModule({
    providers: [
      ChampionshipsViewModel,
      { provide: LoadChampionshipsUseCase, useValue: { run } },
      { provide: CreateChampionshipUseCase, useValue: { run: createRun } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
      { provide: AuthSessionStore, useValue: { role: () => opts.role ?? 'head_coach' } },
      { provide: NotificationService, useValue: notify },
      { provide: TranslateService, useValue: { t: (k: string) => k } },
    ],
  });
  return { vm: TestBed.inject(ChampionshipsViewModel), run, createRun, notify };
}

function fillForm(vm: ChampionshipsViewModel): void {
  vm.setNewName('Summer Cup');
  vm.setNewStart('2024-06-01');
  vm.setNewEnd('2024-06-03');
  vm.setNewLocation('Cairo');
}

describe('ChampionshipsViewModel', () => {
  it('loads all events and counts them', async () => {
    const { vm } = setup();
    await vm.load();
    expect(vm.loading()).toBe(false);
    expect(vm.count()).toBe(2);
  });

  it('overlap-filters by the From/To period', async () => {
    const { vm } = setup();
    await vm.load();
    vm.setFrom('2023-11-01');            // excludes the Oct meet
    expect(vm.count()).toBe(1);
    expect(vm.filtered()[0].nameEn).toBe('Nov Meet');
    vm.setFrom(''); vm.setTo('2023-10-31'); // now only the Oct meet
    expect(vm.count()).toBe(1);
    expect(vm.filtered()[0].nameEn).toBe('Oct Meet');
  });

  it('clearFilter resets the period', async () => {
    const { vm } = setup();
    await vm.load();
    vm.setFrom('2024-01-01');
    expect(vm.count()).toBe(0);
    vm.clearFilter();
    expect(vm.hasFilter()).toBe(false);
    expect(vm.count()).toBe(2);
  });

  it('picks a badge class by status code', async () => {
    const { vm } = setup();
    await vm.load();
    expect(vm.statusBadgeClass(sample[0])).toContain('sky');       // upcoming
    expect(vm.statusBadgeClass(sample[1])).toContain('emerald');   // completed
  });

  it('sets error on failure', async () => {
    const { vm } = setup({ loadRun: jest.fn().mockResolvedValue({ ok: false, error: new Error('x') }) });
    await vm.load();
    expect(vm.error()).toBe(true);
    expect(vm.count()).toBe(0);
  });

  it('canManage is true for head_coach', () => {
    expect(setup({ role: 'head_coach' }).vm.canManage()).toBe(true);
  });

  it('canManage is true for captain', () => {
    expect(setup({ role: 'captain' }).vm.canManage()).toBe(true);
  });

  it('canManage is false for a swimmer', () => {
    expect(setup({ role: 'swimmer' }).vm.canManage()).toBe(false);
  });

  it('formValid requires name, location and end not before start', () => {
    const { vm } = setup();
    expect(vm.formValid()).toBe(false);
    vm.setNewName('Cup'); vm.setNewLocation('Cairo');
    vm.setNewStart('2024-06-03'); vm.setNewEnd('2024-06-01');
    expect(vm.formValid()).toBe(false);   // end before start
    vm.setNewEnd('2024-06-05');
    expect(vm.formValid()).toBe(true);
  });

  it('save creates, reloads the list, closes the form and toasts success', async () => {
    const { vm, run, createRun, notify } = setup();
    await vm.load();
    vm.openForm();
    fillForm(vm);
    run.mockClear();

    await vm.save();

    expect(createRun).toHaveBeenCalledWith({ name: 'Summer Cup', startDate: '2024-06-01', endDate: '2024-06-03', location: 'Cairo' });
    expect(run).toHaveBeenCalledTimes(1);   // list reloaded
    expect(vm.showForm()).toBe(false);
    expect(notify.success).toHaveBeenCalled();
  });

  it('save surfaces an error toast and keeps the form open on failure', async () => {
    const { vm, notify } = setup({ createRun: jest.fn().mockResolvedValue({ ok: false, error: new Error('x') }) });
    await vm.load();
    vm.openForm();
    fillForm(vm);

    await vm.save();

    expect(notify.error).toHaveBeenCalled();
    expect(vm.showForm()).toBe(true);
  });

  it('save is a no-op when the user cannot manage', async () => {
    const createRun = jest.fn();
    const { vm } = setup({ role: 'swimmer', createRun });
    vm.openForm();
    fillForm(vm);
    await vm.save();
    expect(createRun).not.toHaveBeenCalled();
  });

  it('cancelForm resets fields and hides the form', () => {
    const { vm } = setup();
    vm.openForm();
    fillForm(vm);
    vm.cancelForm();
    expect(vm.showForm()).toBe(false);
    expect(vm.newName()).toBe('');
    expect(vm.newLocation()).toBe('');
    expect(vm.formValid()).toBe(false);
  });
});
