import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-decor-background',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (variant() === 'auth') {
      <div
        aria-hidden="true"
        [class]="'pointer-events-none absolute inset-0 overflow-hidden ' + className()">

        <div class="bg-grid-dots absolute inset-0 opacity-[0.15]"></div>
        <!-- Large gradient blobs -->
        <div class="animate-float absolute -top-32 -right-24 h-96 w-96 rounded-full bg-primary/30 blur-3xl"></div>
        <div class="animate-float-slow absolute top-1/3 -left-32 h-[28rem] w-[28rem] rounded-full bg-accent/25 blur-3xl"></div>
        <div class="animate-float-reverse absolute -bottom-40 right-1/4 h-80 w-80 rounded-full bg-success/20 blur-3xl"></div>
        <!-- Floating shapes -->
        <div class="animate-float absolute left-[12%] top-[18%] h-16 w-16 rounded-2xl border border-white/30 bg-white/10 backdrop-blur-sm"></div>
        <div class="animate-float-reverse absolute right-[16%] top-[28%] h-10 w-10 rounded-full border border-white/40 bg-white/10"></div>
        <div class="animate-rotate-slow absolute bottom-[20%] left-[20%] h-12 w-12 border-2 border-white/20"></div>
        <div
          class="animate-float-slow absolute right-[10%] bottom-[14%] h-0 w-0 border-x-[22px] border-b-[36px] border-x-transparent border-b-white/15"
          [style.filter]="'blur(0.5px)'"></div>
      </div>
    } @else {
      <div
        aria-hidden="true"
        [class]="'pointer-events-none absolute inset-0 -z-0 overflow-hidden ' + className()">

        <div class="bg-grid-dots absolute inset-0 opacity-[0.07]"></div>
        <!-- Corner gradient orbs -->
        <div class="animate-float absolute -top-24 -right-20 h-72 w-72 rounded-full bg-primary/10 blur-3xl"></div>
        <div class="animate-float-slow absolute -bottom-28 -left-24 h-80 w-80 rounded-full bg-accent/10 blur-3xl"></div>
        <div class="animate-float-reverse absolute top-1/2 right-1/3 h-56 w-56 rounded-full bg-success/[0.06] blur-3xl"></div>
        <!-- Subtle floating shapes -->
        <div class="animate-float absolute left-[8%] top-[22%] h-10 w-10 rounded-xl border border-primary/15 bg-primary/[0.04]"></div>
        <div class="animate-rotate-slow absolute right-[12%] top-[40%] h-8 w-8 border border-accent/20"></div>
      </div>
    }
  `,
  styles: []
})
export class DecorBackgroundComponent {
  variant = input<'page' | 'auth'>('page');
  className = input<string>('');
}
