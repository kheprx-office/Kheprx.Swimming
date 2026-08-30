import { Component, OnInit, inject } from '@angular/core';
import {
  LucideDynamicIcon,
  LucidePlus,
  LucideSearch,
  LucidePencil,
  LucidePower,
  LucideX,
  LucideLoader2,
} from '@lucide/angular';
import { UsersViewModel } from './users.viewmodel';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';
import { ROLE_LABELS } from '@core/domain/roles';
import { GENDER_LABELS, formatCompensation } from '@features/user-management/domain/model/user-management/user';
import { stripArabic } from '@core/text/arabic';

@Component({
  selector: 'app-user-management-page',
  standalone: true,
  imports: [LucideDynamicIcon, DecorBackgroundComponent],
  templateUrl: './user-management.page.html',
})
export class UserManagementPage implements OnInit {
  protected readonly vm = inject(UsersViewModel);
  readonly ROLE_LABELS = ROLE_LABELS;
  readonly GENDER_LABELS = GENDER_LABELS;
  readonly formatCompensation = formatCompensation;
  protected readonly stripArabic = stripArabic;
  readonly PlusIcon = LucidePlus;
  readonly SearchIcon = LucideSearch;
  readonly PencilIcon = LucidePencil;
  readonly PowerIcon = LucidePower;
  readonly XIcon = LucideX;
  readonly Loader2Icon = LucideLoader2;

  ngOnInit(): void { void this.vm.init(); }
}
