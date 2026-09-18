import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { DateFieldComponent } from '@core/ui/components/date-field.component';
import { ChipGroupComponent } from '@core/ui/components/chip-group.component';
import { RegisterSwimmerViewModel } from './register-swimmer.viewmodel';

@Component({
  selector: 'app-register-swimmer-form',
  standalone: true,
  imports: [TranslatePipe, TextFieldComponent, SelectFieldComponent, DateFieldComponent, ChipGroupComponent],
  templateUrl: './register-swimmer-form.component.html',
})
export class RegisterSwimmerFormComponent {
  protected readonly vm = inject(RegisterSwimmerViewModel);
}
