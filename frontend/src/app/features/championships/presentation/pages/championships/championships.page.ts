import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@core/i18n';
import { ChampionshipsViewModel } from './championships.viewmodel';

@Component({
  selector: 'app-championships-page',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslatePipe],
  templateUrl: './championships.page.html',
})
export class ChampionshipsPage implements OnInit {
  readonly vm = inject(ChampionshipsViewModel);
  ngOnInit(): void { void this.vm.load(); }
}
