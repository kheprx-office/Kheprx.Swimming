import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet, RouterLink } from '@angular/router';
import {
  LucideDynamicIcon,
  LucideHome,
  LucideShieldCheck,
  LucideSettings,
  LucideLogOut,
  LucideMenu,
  LucideX,
  LucideSearch,
} from '@lucide/angular';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { UserRole } from '@core/domain/roles';
import { ROLE_LABELS } from '@core/domain/roles';

// LucideIconType: the union accepted by @lucide/angular's [lucideIcon] binding.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type LucideIconType = any;

interface NavItem {
  label: string;
  icon: LucideIconType;
  route?: string;
  roles?: UserRole[];
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, LucideDynamicIcon],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);

  readonly MenuIcon = LucideMenu;
  readonly XIcon = LucideX;
  readonly SearchIcon = LucideSearch;
  readonly LogOutIcon = LucideLogOut;

  readonly isSidebarOpen = signal(true);
  readonly isMobileMenuOpen = signal(false);

  readonly userInitial = computed(
    () => (this.auth.currentUserName() || '؟').trim().slice(0, 1) || 'م',
  );
  readonly roleLabel = computed(() => {
    const r = this.auth.role();
    return r ? ROLE_LABELS[r] : '';
  });

  // Active items navigate; inert items (route omitted) render disabled ("قريباً").
  private readonly allItems: NavItem[] = [
    { label: 'الرئيسية', icon: LucideHome, route: '/home' },
    {
      label: 'إدارة المستخدمين',
      icon: LucideShieldCheck,
      route: '/user-management',
      roles: ['admin'],
    },
    { label: 'الإعدادات', icon: LucideSettings, route: '/account' },
  ];
  readonly navItems = computed<NavItem[]>(() => {
    const role = this.auth.role();
    return this.allItems.filter((i) => !i.roles || (role !== null && i.roles.includes(role)));
  });

  isActive(route?: string): boolean {
    return !!route && this.router.url.startsWith(route);
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
    void this.router.navigate(['/login']);
  }
}
