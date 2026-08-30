import { Routes } from '@angular/router';
import { authGuard, roleGuard } from '@features/auth/presentation/auth.guard';
import { LayoutComponent } from './layout/layout.component';
import { LoginViewModel, ChangePasswordViewModel, AccountViewModel } from '@features/auth';
import { UsersViewModel } from '@features/user-management';

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
      { path: 'home', loadComponent: () => import('@features/home').then((m) => m.HomePage) },
      {
        path: 'account',
        loadComponent: () => import('@features/auth').then((m) => m.AccountPage),
        providers: [AccountViewModel],
      },
      {
        path: 'change-password',
        loadComponent: () => import('@features/auth').then((m) => m.ChangePasswordPage),
        providers: [ChangePasswordViewModel],
      },
      {
        path: 'user-management',
        canActivate: [roleGuard('admin')],
        loadComponent: () => import('@features/user-management').then((m) => m.UserManagementPage),
        providers: [UsersViewModel],
      },
      { path: '', pathMatch: 'full', redirectTo: 'home' },
    ],
  },
  { path: '**', redirectTo: '' },
];
