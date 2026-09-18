import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { HealthMonitoringViewModel } from './health-monitoring.viewmodel';

@Component({
  selector: 'app-health-monitoring-page',
  standalone: true,
  imports: [TranslatePipe, SelectFieldComponent, TextFieldComponent],
  templateUrl: './health-monitoring.page.html',
})
export class HealthMonitoringPage {
  protected readonly vm = inject(HealthMonitoringViewModel);
}
