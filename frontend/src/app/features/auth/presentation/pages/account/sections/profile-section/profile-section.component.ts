import { Component, Input } from '@angular/core';
import { LucideDynamicIcon, LucideShield } from '@lucide/angular';
import { TranslatePipe } from '@core/i18n';
import { ROLE_LABELS } from '@core/domain/roles';
import { UserRole } from '@core/domain/roles';
import { SkeletonComponent } from '@core/ui/components/skeleton.component';

@Component({
  selector: 'app-profile-section',
  standalone: true,
  imports: [LucideDynamicIcon, TranslatePipe, SkeletonComponent],
  templateUrl: './profile-section.component.html',
})
export class ProfileSection {
  @Input() displayName: string = '';
  @Input() email: string = '';
  @Input() role: UserRole | null = null;
  @Input() phone: string | null = null;
  @Input() age: number | null = null;
  @Input() genderKey: string | null = null;
  @Input() nationalId: string | null = null;
  @Input() initial: string = '?';
  @Input() loaded: boolean = false;

  protected readonly ROLE_LABELS = ROLE_LABELS;
  protected readonly ShieldIcon = LucideShield;
}
