import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { AttendanceEntryViewModel, EntryRow } from './attendance.viewmodel';

@Component({
  selector: 'app-attendance-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './attendance.page.html',
})
export class AttendancePage implements OnInit {
  protected readonly vm = inject(AttendanceEntryViewModel);
  private readonly language = inject(LanguageStore);

  ngOnInit(): void { void this.vm.load(); }

  displayName(r: EntryRow): string {
    return this.language.lang() === 'ar' ? (r.nameAr ?? r.nameEn) : r.nameEn;
  }

  initials(r: EntryRow): string {
    const parts = r.nameEn.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }
}
