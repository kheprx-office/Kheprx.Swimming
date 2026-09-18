import { Component } from '@angular/core';
import { TranslatePipe } from '@core/i18n';

// Placeholder page — renders through the shared app shell (sidebar + header) so the
// Championships nav item is fully navigable. Feature content is intentionally not built yet.
@Component({
  selector: 'app-championships-page',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './championships.page.html',
})
export class ChampionshipsPage {}
