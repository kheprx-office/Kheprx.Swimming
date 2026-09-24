import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { DashboardPage } from '@features/dashboard/presentation/pages/dashboard/dashboard.page';
import { DashboardViewModel } from '@features/dashboard/presentation/pages/dashboard/dashboard.viewmodel';

describe('DashboardPage', () => {
  it('loads the summary on init', () => {
    const vm = {
      load: jest.fn().mockResolvedValue(undefined),
      loading: signal(false),
      error: signal(false),
      summary: signal(null),
      currentUserName: signal('John'),
    };
    TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [{ provide: DashboardViewModel, useValue: vm }],
    });
    TestBed.overrideComponent(DashboardPage, { set: { providers: [] } });
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    expect(vm.load).toHaveBeenCalled();
  });
});
