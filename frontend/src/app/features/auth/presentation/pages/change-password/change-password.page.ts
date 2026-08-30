import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { ChangePasswordViewModel } from './change-password.viewmodel';

@Component({
  selector: 'app-change-password-page',
  standalone: true,
  imports: [FormsModule, DecorBackgroundComponent],
  templateUrl: './change-password.page.html',
})
export class ChangePasswordPage {
  protected readonly vm = inject(ChangePasswordViewModel);
}
