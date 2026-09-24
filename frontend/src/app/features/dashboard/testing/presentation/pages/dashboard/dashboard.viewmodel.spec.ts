import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { DashboardViewModel } from '@features/dashboard/presentation/pages/dashboard/dashboard.viewmodel';
import { LoadDashboardSummaryUseCase } from '@features/dashboard/domain/usecases/load-dashboard-summary.use-case';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { DashboardSummary } from '@features/dashboard/domain/model/dashboard-summary';

const SUMMARY: DashboardSummary = {
  swimmerCount: 452,
  newThisMonth: 12,
  monthAttendanceRatePct: 88,
  last7Days: [
    { date: '2026-09-27', present: 8, absent: 2 },
    { date: '2026-09-28', present: 0, absent: 0 },
  ],
  strokeSplit: [
    { strokeId: 's1', code: 'free', nameEn: 'Freestyle', nameAr: 'حرة', count: 9 },
    { strokeId: 's2', code: 'back', nameEn: 'Backstroke', nameAr: 'ظهر', count: 3 },
  ],
};

function build(overrides: { load?: unknown } = {}) {
  const loadUc = { run: jest.fn().mockResolvedValue(overrides.load ?? { ok: true, data: SUMMARY }) };
  const auth = { currentUserName: signal('John Coach') };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      DashboardViewModel,
      { provide: LoadDashboardSummaryUseCase, useValue: loadUc },
      { provide: AuthSessionStore, useValue: auth },
    ],
  });
  return { vm: TestBed.inject(DashboardViewModel), loadUc };
}

describe('DashboardViewModel', () => {
  it('load() populates summary on success and clears loading', async () => {
    const { vm } = build();
    await vm.load();
    expect(vm.summary()).toEqual(SUMMARY);
    expect(vm.loading()).toBe(false);
    expect(vm.error()).toBe(false);
  });

  it('load() sets error and null summary on failure', async () => {
    const { vm } = build({ load: { ok: false } });
    await vm.load();
    expect(vm.summary()).toBeNull();
    expect(vm.error()).toBe(true);
  });

  it('exposes the current user name from the auth store', () => {
    const { vm } = build();
    expect(vm.currentUserName()).toBe('John Coach');
  });
});
