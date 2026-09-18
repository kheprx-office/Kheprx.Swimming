import { Component, inject } from '@angular/core';
import { TranslatePipe, LanguageStore } from '@core/i18n';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { MedicalTest } from '@features/medical-tests/domain/model/medical-test';
import { MedicalTestsViewModel } from './medical-tests.viewmodel';

@Component({
  selector: 'app-medical-tests-page',
  standalone: true,
  imports: [TranslatePipe, TextFieldComponent],
  templateUrl: './medical-tests.page.html',
})
export class MedicalTestsPage {
  protected readonly vm = inject(MedicalTestsViewModel);
  private readonly lang = inject(LanguageStore);

  protected displayName(t: MedicalTest): string {
    return this.lang.lang() === 'ar' ? t.nameAr : t.nameEn;
  }
}
