import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslatePipe } from '@core/i18n';
import { LanguageStore } from '@core/i18n/language.store';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';
import { TextFieldComponent } from '@core/ui/components/text-field.component';
import { SwimmerProfileViewModel } from './swimmer-profile.viewmodel';

interface ProfileTab { key: string; labelKey: string; }

@Component({
  selector: 'app-swimmer-profile-page',
  standalone: true,
  imports: [TranslatePipe, SelectFieldComponent, TextFieldComponent],
  templateUrl: './swimmer-profile.page.html',
})
export class SwimmerProfilePage implements OnInit {
  protected readonly vm = inject(SwimmerProfileViewModel);
  private readonly route = inject(ActivatedRoute);
  protected readonly language = inject(LanguageStore);

  // Full tab strip for visual fidelity; identityVitals, guardian, physiological, inbody, records, healthMonitoring, attendance, and feedback are enabled.
  protected readonly enabledTabs = new Set(['identityVitals', 'guardian', 'physiological', 'inbody', 'records', 'healthMonitoring', 'attendance', 'feedback']);
  isEnabled(key: string): boolean { return this.enabledTabs.has(key); }

  protected readonly tabs: ProfileTab[] = [
    { key: 'identityVitals', labelKey: 'swimmerProfile.tabs.identityVitals' },
    { key: 'guardian', labelKey: 'swimmerProfile.tabs.guardian' },
    { key: 'physiological', labelKey: 'swimmerProfile.tabs.physiological' },
    { key: 'inbody', labelKey: 'swimmerProfile.tabs.inbody' },
    { key: 'records', labelKey: 'swimmerProfile.tabs.records' },
    { key: 'healthMonitoring', labelKey: 'swimmerProfile.tabs.healthMonitoring' },
    { key: 'attendance', labelKey: 'swimmerProfile.tabs.attendance' },
    { key: 'championships', labelKey: 'swimmerProfile.tabs.championships' },
    { key: 'feedback', labelKey: 'swimmerProfile.tabs.feedback' },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    void this.vm.load(id);
  }

  displayName(): string {
    const i = this.vm.profile()?.identity;
    if (!i) return '';
    return this.language.lang() === 'ar' ? (i.nameAr ?? i.nameEn) : i.nameEn;
  }

  fmtDate(iso: string): string { return iso ? iso.slice(0, 10) : '—'; }

  refLabel(ref: { nameEn: string; nameAr: string | null } | null | undefined): string {
    if (!ref) return '—';
    return this.language.lang() === 'ar' ? (ref.nameAr ?? ref.nameEn) : ref.nameEn;
  }

  testName(r: { testNameEn: string; testNameAr: string }): string {
    return this.language.lang() === 'ar' ? (r.testNameAr || r.testNameEn) : r.testNameEn;
  }

  protected readonly starScale = [1, 2, 3, 4, 5];

  feedbackCategoryName(categoryId: string): string {
    const c = this.vm.feedbackCategories().find((x) => x.id === categoryId);
    return this.refLabel(c ?? null);
  }

  monthLabel(ym: string): string {
    if (!ym) return '';
    const [y, m] = ym.split('-').map(Number);
    const d = new Date(y, m - 1, 1);
    return d.toLocaleDateString(this.language.lang() === 'ar' ? 'ar-EG' : 'en-US', { month: 'long', year: 'numeric' });
  }

  // Calendar cell tint by status code (present=blue, late=amber, absent=red, excused=blue/info).
  attCellClass(code: string | null): string {
    switch (code) {
      case 'present': return 'bg-blue-500/10 text-blue-600';
      case 'late': return 'bg-amber-500/10 text-amber-600';
      case 'absent': return 'bg-red-500/10 text-red-600';
      case 'excused': return 'bg-blue-500/10 text-blue-600';
      default: return 'text-text-secondary';
    }
  }

  attDotClass(code: string): string {
    switch (code) {
      case 'present': return 'bg-blue-500';
      case 'late': return 'bg-amber-500';
      case 'absent': return 'bg-red-500';
      case 'excused': return 'bg-blue-500';
      default: return 'bg-border';
    }
  }

  initials(): string {
    const en = this.vm.profile()?.identity.nameEn ?? '';
    const parts = en.trim().split(/\s+/).filter(Boolean);
    return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase();
  }
}
