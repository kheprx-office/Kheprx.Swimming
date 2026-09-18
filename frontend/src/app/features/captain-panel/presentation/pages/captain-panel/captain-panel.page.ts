import { Component } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  LucideDynamicIcon,
  LucideUserPlus,
  LucideDatabase,
  LucideFlaskConical,
  LucideHeartPulse,
  LucideArrowRight,
} from '@lucide/angular';
import { TranslatePipe } from '@core/i18n';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type LucideIconType = any;

interface PanelCard {
  // i18n key stem under captainPanel.cards.<key>.{title,meta,desc}
  key: string;
  icon: LucideIconType;
  // Only cards whose feature exists get a route; others are safe non-navigating placeholders.
  route?: string;
}

// Captain Panel dashboard: an overview of the administration tools. Account Creation is live
// (route exists); the other cards are placeholders until their features are built. Reachable by
// head_coach + captain (roleGuard) — the cards themselves carry no privileged action.
@Component({
  selector: 'app-captain-panel-page',
  standalone: true,
  imports: [TranslatePipe, RouterLink, NgTemplateOutlet, LucideDynamicIcon],
  templateUrl: './captain-panel.page.html',
})
export class CaptainPanelPage {
  protected readonly ArrowRightIcon = LucideArrowRight;

  protected readonly cards: PanelCard[] = [
    { key: 'accountCreation', icon: LucideUserPlus, route: '/captain-panel/account-creation' },
    { key: 'swimmerRecords', icon: LucideDatabase },
    { key: 'medicalTests', icon: LucideFlaskConical },
    { key: 'healthMonitoring', icon: LucideHeartPulse },
  ];
}
