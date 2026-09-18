import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SwimmerDataViewModel } from './swimmer-data.viewmodel';

@Component({
  selector: 'app-swimmer-data-page',
  standalone: true,
  imports: [TranslatePipe, SelectFieldComponent, TextFieldComponent],
  templateUrl: './swimmer-data.page.html',
})
export class SwimmerDataPage {
  protected readonly vm = inject(SwimmerDataViewModel);
}
