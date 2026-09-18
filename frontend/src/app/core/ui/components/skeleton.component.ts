// SkeletonComponent: a single shimmer placeholder block. Sizing comes from the consumer,
// via width/height inputs or Tailwind utility classes on the host. Wraps the global
// `.skeleton` shimmer so every loading placeholder looks identical.
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-skeleton',
  standalone: true,
  template: `<span class="skeleton block" [style.width]="width" [style.height]="height"></span>`,
})
export class SkeletonComponent {
  @Input() width = '100%';
  @Input() height = '1rem';
}
