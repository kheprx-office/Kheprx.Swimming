import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { ChampionshipDetailViewModel, DetailTab } from './championship-detail.viewmodel';
import { CompetitionDaysViewModel } from './competition-days.viewmodel';
import { RaceResultsViewModel } from './race-results.viewmodel';

interface DetailTabDef { key: DetailTab; labelKey: string; }

@Component({
  selector: 'app-championship-detail-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe, RouterLink],
  templateUrl: './championship-detail.page.html',
  providers: [CompetitionDaysViewModel, RaceResultsViewModel],
})
export class ChampionshipDetailPage implements OnInit {
  readonly vm = inject(ChampionshipDetailViewModel);
  readonly daysVm = inject(CompetitionDaysViewModel);
  readonly resultsVm = inject(RaceResultsViewModel);
  private readonly route = inject(ActivatedRoute);

  private eventId = '';

  // All four tabs are live.
  protected readonly enabledTabs = new Set<DetailTab>(['enrollment', 'days', 'finished', 'results']);
  isEnabled(key: DetailTab): boolean { return this.enabledTabs.has(key); }

  protected readonly tabs: DetailTabDef[] = [
    { key: 'enrollment', labelKey: 'championships.detail.tabs.enrollment' },
    { key: 'days', labelKey: 'championships.detail.tabs.days' },
    { key: 'finished', labelKey: 'championships.detail.tabs.finished' },
    { key: 'results', labelKey: 'championships.detail.tabs.results' },
  ];

  ngOnInit(): void {
    this.eventId = this.route.snapshot.paramMap.get('id') ?? '';
    void this.vm.load(this.eventId);
  }

  onTab(key: DetailTab): void {
    if (!this.isEnabled(key)) return;
    this.vm.setTab(key);
    if (key === 'days') {
      const c = this.vm.championship();
      void this.daysVm.ensureLoaded(this.eventId, c?.startDate ?? '', c?.endDate ?? '');
    } else if (key === 'finished' || key === 'results') {
      void this.resultsVm.ensureLoaded(this.eventId);
    }
  }
}
