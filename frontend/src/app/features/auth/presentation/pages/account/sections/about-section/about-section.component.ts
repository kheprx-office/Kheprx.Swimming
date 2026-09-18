import { Component } from '@angular/core';
import { LucideDynamicIcon, LucideInfo } from '@lucide/angular';
import { TranslatePipe } from '@core/i18n';

@Component({
  selector: 'app-about-section',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe],
  templateUrl: './about-section.component.html',
})
export class AboutSection {
  protected readonly InfoIcon = LucideInfo;
}
