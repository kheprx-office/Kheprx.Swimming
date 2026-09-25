import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet, RouterLink } from '@angular/router';
import {
  LucideDynamicIcon,
  LucideLayoutDashboard,
  LucideUsers,
  LucideCalendarCheck,
  LucideTrophy,
  LucideShieldAlert,
  LucideSettings,
  LucideSun,
  LucideMoon,
  LucideLogOut,
  LucideMenu,
  LucideX,
  LucideSearch,
  LucideWaves,
  LucideMapPin,
  LucidePanelLeftClose,
  LucideLock,
  LucideUser,
} from '@lucide/angular';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';
import { UserRole } from '@core/domain/roles';
import { ROLE_LABELS } from '@core/domain/roles';
import { TranslatePipe, LanguageStore, type Lang } from '@core/i18n';
import { ThemeStore } from '@core/ui/theme/theme.store';

// LucideIconType: the union accepted by @lucide/angular's [lucideIcon] binding.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type LucideIconType = any;

interface NavItem {
  label: string;
  icon: LucideIconType;
  route?: string;
  roles?: UserRole[];
}

interface NavGroup {
  header: string;
  items: NavItem[];
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, LucideDynamicIcon, TranslatePipe],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {
  private readonly auth = inject(AuthSessionStore);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageStore);
  private readonly theme = inject(ThemeStore);

  readonly MenuIcon = LucideMenu;
  readonly XIcon = LucideX;
  readonly SearchIcon = LucideSearch;
  readonly LogOutIcon = LucideLogOut;
  readonly SunIcon = LucideSun;
  readonly MoonIcon = LucideMoon;
  readonly WavesIcon = LucideWaves;
  readonly MapPinIcon = LucideMapPin;
  readonly CollapseIcon = LucidePanelLeftClose;
  readonly LockIcon = LucideLock;

  // While a forced first-login password change is pending, firstLoginGuard pins the user
  // to /change-password. Mirror that here to lock the nav (defense in depth) so the sidebar
  // doesn't look interactive-but-broken — clicks would otherwise just bounce back.
  readonly mustChangePassword = this.auth.mustChangePassword;

  readonly lang = this.language.lang;
  readonly mode = this.theme.mode;

  readonly isSidebarOpen = signal(true);
  readonly isMobileMenuOpen = signal(false);

  // Two-letter initials (first + last name) for the account avatar, e.g. "Sara Ali" → "SA".
  readonly userInitials = computed(() => {
    const parts = (this.auth.currentUserName() || '').trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return 'U';
    const first = parts[0][0];
    const last = parts.length > 1 ? parts[parts.length - 1][0] : '';
    return (first + last).toUpperCase();
  });
  readonly roleLabel = computed(() => {
    const r = this.auth.role();
    return r ? ROLE_LABELS[r] : '';
  });

  readonly currentUserName = this.auth.currentUserName;

  // Grouped nav structure. Every item routes to a real page — features not built yet
  // navigate to a blank placeholder page rather than rendering as disabled. Role-gated
  // items (e.g. Captain Panel) are filtered out for roles that genuinely lack access.
  private readonly allGroups: NavGroup[] = [
    {
      header: 'shell.nav.groups.overview',
      items: [
        { label: 'shell.nav.myProfile', icon: LucideUser, route: '/my-profile', roles: ['swimmer'] },
        { label: 'shell.nav.dashboard', icon: LucideLayoutDashboard, route: '/home', roles: ['head_coach', 'captain'] },
      ],
    },
    {
      header: 'shell.nav.groups.coaching',
      items: [
        { label: 'shell.nav.swimmers', icon: LucideUsers, route: '/swimmers', roles: ['head_coach', 'captain'] },
        { label: 'shell.nav.attendance', icon: LucideCalendarCheck, route: '/attendance', roles: ['head_coach', 'captain'] },
        { label: 'shell.nav.championships', icon: LucideTrophy, route: '/championships', roles: ['head_coach', 'captain'] },
      ],
    },
    {
      header: 'shell.nav.groups.administration',
      items: [
        { label: 'shell.nav.captainPanel', icon: LucideShieldAlert, roles: ['head_coach', 'captain'], route: '/captain-panel' },
        { label: 'shell.nav.settings', icon: LucideSettings, route: '/account' },
      ],
    },
  ];

  readonly navGroups = computed<NavGroup[]>(() => {
    const role = this.auth.role();
    return this.allGroups
      .map((g) => ({ ...g, items: g.items.filter((i) => !i.roles || (role !== null && i.roles.includes(role))) }))
      .filter((g) => g.items.length > 0);
  });

  // Task 8: breadcrumb derived from current route and navGroups
  readonly breadcrumb = computed(() => {
    const url = this.router.url;
    for (const g of this.navGroups()) {
      const item = g.items.find((i) => i.route && url.startsWith(i.route));
      if (item) return { group: g.header, label: item.label };
    }
    return { group: 'shell.nav.groups.administration', label: 'shell.nav.settings' };
  });

  toggleLanguage(): void {
    this.language.toggle();
  }

  setLang(lang: Lang): void {
    this.language.set(lang);
  }

  toggleTheme(): void {
    this.theme.toggle();
  }

  isActive(route?: string): boolean {
    return !!route && this.router.url.startsWith(route);
  }

  async signOut(): Promise<void> {
    await this.auth.signOut();
    void this.router.navigate(['/login']);
  }
}
