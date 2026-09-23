import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { ChampionshipDetailViewModel, DetailTab } from './championship-detail.viewmodel';

interface DetailTabDef { key: DetailTab; labelKey: string; }

@Component({
  selector: 'app-championship-detail-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, RouterLink],
  templateUrl: './championship-detail.page.html',
})
export class ChampionshipDetailPage implements OnInit {
  readonly vm = inject(ChampionshipDetailViewModel);
  private readonly route = inject(ActivatedRoute);

  // Full strip for visual fidelity; only 'enrollment' is enabled this pass.
  protected readonly enabledTabs = new Set<DetailTab>(['enrollment']);
  isEnabled(key: DetailTab): boolean { return this.enabledTabs.has(key); }

  protected readonly tabs: DetailTabDef[] = [
    { key: 'enrollment', labelKey: 'championships.detail.tabs.enrollment' },
    { key: 'days', labelKey: 'championships.detail.tabs.days' },
    { key: 'finished', labelKey: 'championships.detail.tabs.finished' },
    { key: 'results', labelKey: 'championships.detail.tabs.results' },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    void this.vm.load(id);
  }
}
