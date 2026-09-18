import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@core/i18n';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { DateFieldComponent } from '@core/ui/components/date-field.component';
import { RegisterCoachViewModel } from './register-coach.viewmodel';

@Component({
  selector: 'app-register-coach-form',
  standalone: true,
  imports: [TranslatePipe, TextFieldComponent, SelectFieldComponent, DateFieldComponent],
  templateUrl: './register-coach-form.component.html',
})
export class RegisterCoachFormComponent {
  protected readonly vm = inject(RegisterCoachViewModel);
}
