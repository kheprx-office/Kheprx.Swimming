import { Component, Input, inject } from '@angular/core';
import { LucideDynamicIcon, LucideLanguages, LucideMoon, LucideSun } from '@lucide/angular';
import { LanguageStore, TranslatePipe } from '@core/i18n';
import { ThemeStore } from '@core/ui/theme/theme.store';

@Component({
  selector: 'app-preferences-section',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe],
  templateUrl: './preferences-section.component.html',
})
export class PreferencesSection {
  @Input() disabled = false;

  private readonly language = inject(LanguageStore);
  private readonly theme = inject(ThemeStore);

  protected readonly lang = this.language.lang;
  protected readonly mode = this.theme.mode;

  protected readonly LangIcon = LucideLanguages;
  protected readonly SunIcon = LucideSun;
  protected readonly MoonIcon = LucideMoon;

  toggleLanguage(): void { this.language.toggle(); }
  setTheme(mode: 'light' | 'dark'): void { this.theme.set(mode); }
}
