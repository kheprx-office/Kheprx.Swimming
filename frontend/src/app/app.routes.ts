import { Routes } from '@angular/router';
import { authGuard, firstLoginGuard, roleGuard } from '@features/auth/presentation/auth.guard';
import { LayoutComponent } from './layout/layout.component';
import { LoginViewModel, AccountViewModel } from '@features/auth';
import { RegisterSwimmerViewModel, RegisterCoachViewModel } from '@features/captain-panel';

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
      { path: 'home', canActivate: [firstLoginGuard], loadComponent: () => import('@features/home').then((m) => m.HomePage) },
      // Placeholder feature routes — blank pages so the sidebar nav is fully clickable
      // while the real features are still to be built.
      { path: 'swimmers', canActivate: [firstLoginGuard], loadComponent: () => import('@features/swimmers').then((m) => m.SwimmersPage) },
      { path: 'attendance', canActivate: [firstLoginGuard], loadComponent: () => import('@features/attendance').then((m) => m.AttendancePage) },
      { path: 'championships', canActivate: [firstLoginGuard], loadComponent: () => import('@features/championships').then((m) => m.ChampionshipsPage) },
      { path: 'captain-panel', canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')], loadComponent: () => import('@features/captain-panel').then((m) => m.CaptainPanelPage) },
      {
        path: 'captain-panel/account-creation',
        canActivate: [firstLoginGuard, roleGuard('head_coach', 'captain')],
        loadComponent: () => import('@features/captain-panel').then((m) => m.AccountCreationPage),
        providers: [RegisterSwimmerViewModel, RegisterCoachViewModel],
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
