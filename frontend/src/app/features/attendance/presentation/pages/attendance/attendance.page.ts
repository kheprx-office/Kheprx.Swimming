import { Component } from '@angular/core';
import { TranslatePipe } from '@core/i18n';

// Placeholder page — renders through the shared app shell (sidebar + header) so the
// Attendance nav item is fully navigable. Feature content is intentionally not built yet.
@Component({
  selector: 'app-attendance-page',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './attendance.page.html',
})
export class AttendancePage {}
