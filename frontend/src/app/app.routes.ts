import { Routes } from '@angular/router';
import { authGuard, firstLoginGuard, roleGuard } from '@features/auth/presentation/auth.guard';
import { LayoutComponent } from './layout/layout.component';
import { LoginViewModel, AccountViewModel } from '@features/auth';
import { RegisterSwimmerViewModel, RegisterCoachViewModel, MedicalTestsViewModel, HealthMonitoringViewModel, SwimmerDataViewModel } from '@features/captain-panel';
import { SwimmerProfileViewModel } from '@features/swimmer-profile';
import { AttendanceEntryViewModel } from '@features/attendance';
import { ChampionshipsViewModel, ChampionshipDetailViewModel, CompetitionDaysViewModel } from '@features/championships';
import { DashboardViewModel } from '@features/dashboard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('@features/auth').then((m) => m.LoginPage),
    providers: [LoginViewModel],
  },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'home',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/dashboard').then((m) => m.DashboardPage),
        providers: [DashboardViewModel],
      },
      // Placeholder feature routes — blank pages so the sidebar nav is fully clickable
      // while the real features are still to be built.
      { path: 'swimmers', canActivate: [firstLoginGuard], loadComponent: () => import('@features/swimmers').then((m) => m.SwimmersPage) },
      {
        path: 'swimmers/:id',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/swimmer-profile').then((m) => m.SwimmerProfilePage),
        providers: [SwimmerProfileViewModel],
      },
      {
        path: 'attendance',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/attendance').then((m) => m.AttendancePage),
        providers: [AttendanceEntryViewModel],
      },
      {
        path: 'championships',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/championships').then((m) => m.ChampionshipsPage),
        providers: [ChampionshipsViewModel],
      },
      {
        path: 'championships/:id',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/championships').then((m) => m.ChampionshipDetailPage),
        providers: [ChampionshipDetailViewModel, CompetitionDaysViewModel],
      },
      { path: 'captain-panel', canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')], loadComponent: () => import('@features/captain-panel').then((m) => m.CaptainPanelPage) },
      {
        path: 'captain-panel/account-creation',
        canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.AccountCreationPage),
        providers: [RegisterSwimmerViewModel, RegisterCoachViewModel],
      },
      {
        path: 'captain-panel/medical-tests',
        canActivate: [firstLoginGuard, roleGuard('head_coach')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.MedicalTestsPage),
        providers: [MedicalTestsViewModel],
      },
      {
        path: 'captain-panel/health-monitoring',
        canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.HealthMonitoringPage),
        providers: [HealthMonitoringViewModel],
      },
      {
        path: 'captain-panel/swimmer-data',
        canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.SwimmerDataPage),
        providers: [SwimmerDataViewModel],
      },
      {
        path: 'account',
        canActivate: [firstLoginGuard],
        loadComponent: () => import('@features/auth').then((m) => m.AccountPage),
        providers: [AccountViewModel],
      },
      {
        path: 'change-password',
        loadComponent: () => import('@features/auth').then((m) => m.ChangePasswordPage),
      },
      { path: '', pathMatch: 'full', redirectTo: 'home' },
    ],
  },
  { path: '**', redirectTo: '' },
];
