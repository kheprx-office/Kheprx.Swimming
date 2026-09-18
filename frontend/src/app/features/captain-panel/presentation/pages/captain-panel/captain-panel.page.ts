import { Component, computed, inject } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  LucideDynamicIcon, LucideUserPlus, LucideDatabase, LucideFlaskConical, LucideHeartPulse, LucideArrowRight,
} from '@lucide/angular';
import { TranslatePipe } from '@core/i18n';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type LucideIconType = any;

interface PanelCard {
  key: string;
  icon: LucideIconType;
  route?: string;
}

@Component({
  selector: 'app-captain-panel-page',
  standalone: true,
  imports: [TranslatePipe, RouterLink, NgTemplateOutlet, LucideDynamicIcon],
  templateUrl: './captain-panel.page.html',
})
export class CaptainPanelPage {
  private readonly auth = inject(AuthSessionStore);
  protected readonly ArrowRightIcon = LucideArrowRight;

  // Medical Tests is head-coach-managed: the card is only shown to head coaches.
  protected readonly cards = computed<PanelCard[]>(() => {
    const all: PanelCard[] = [
      { key: 'accountCreation', icon: LucideUserPlus, route: '/captain-panel/account-creation' },
      { key: 'swimmerRecords', icon: LucideDatabase },
      { key: 'medicalTests', icon: LucideFlaskConical, route: '/captain-panel/medical-tests' },
      { key: 'healthMonitoring', icon: LucideHeartPulse },
    ];
    return this.auth.role() === 'head_coach' ? all : all.filter((c) => c.key !== 'medicalTests');
  });
}
