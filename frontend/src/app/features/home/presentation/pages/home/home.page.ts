import { Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  LucideDynamicIcon,
  LucideShieldCheck,
  LucideSettings,
} from '@lucide/angular';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { UserRole } from '@core/domain/roles';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type LucideIconType = any;

interface HomeCard {
  label: string;
  desc: string;
  icon: LucideIconType;
  color: string;
  route?: string;
  roles?: UserRole[];
}

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [LucideDynamicIcon, DecorBackgroundComponent],
  templateUrl: './home.page.html',
})
export class HomePage {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);

  readonly currentUserName = this.auth.currentUserName;

  // Section launchpad — starter scaffold cards only. Cards with a `route` navigate
  // (role-gated like the sidebar's navItems); business feature cards were removed
  // since those routes do not exist in this starter.
  private readonly allCards: HomeCard[] = [
    { label: 'إدارة المستخدمين', desc: 'المستخدمون والصلاحيات', icon: LucideShieldCheck, color: 'from-danger to-rose-400',     route: '/user-management', roles: ['admin'] },
    { label: 'الإعدادات',        desc: 'الملف الشخصي والتفضيلات', icon: LucideSettings,    color: 'from-slate-600 to-slate-400', route: '/account' },
  ];

  readonly cards = computed<HomeCard[]>(() => {
    const role = this.auth.role();
    return this.allCards.filter((c) => !c.roles || (role !== null && c.roles.includes(role)));
  });

  go(route?: string): void {
    if (route) void this.router.navigate([route]);
  }
}
