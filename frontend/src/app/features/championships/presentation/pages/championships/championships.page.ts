import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@core/i18n';
import { ChampionshipsViewModel } from './championships.viewmodel';

@Component({
  selector: 'app-championships-page',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  templateUrl: './championships.page.html',
})
export class ChampionshipsPage implements OnInit {
  readonly vm = inject(ChampionshipsViewModel);
  ngOnInit(): void { void this.vm.load(); }
}
