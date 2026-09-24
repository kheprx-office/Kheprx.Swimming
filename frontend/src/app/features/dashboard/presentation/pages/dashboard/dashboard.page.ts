import { Component, OnInit, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { DashboardViewModel } from './dashboard.viewmodel';
import { DashboardHeroComponent } from './components/dashboard-hero.component';
import { WeeklyAttendanceChartComponent } from './components/weekly-attendance-chart.component';
import { SwimmersByStrokeComponent } from './components/swimmers-by-stroke.component';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [
    TranslatePipe,
    DecorBackgroundComponent,
    DashboardHeroComponent,
    WeeklyAttendanceChartComponent,
    SwimmersByStrokeComponent,
  ],
  templateUrl: './dashboard.page.html',
  providers: [DashboardViewModel],
})
export class DashboardPage implements OnInit {
  protected readonly vm = inject(DashboardViewModel);

  ngOnInit(): void {
    void this.vm.load();
  }
}
