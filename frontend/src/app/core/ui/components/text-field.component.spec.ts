import { TestBed } from '@angular/core/testing';
import { Component, signal } from '@angular/core';
import { TextFieldComponent } from './text-field.component';

@Component({
  standalone: true,
  imports: [TextFieldComponent],
  template: `<app-text-field [label]="'Name'" [value]="val()" (valueChange)="val.set($event)" />`,
})
class HostComponent {
  val = signal('');
}

describe('TextFieldComponent', () => {
  it('renders the label', () => {
    const f = TestBed.createComponent(HostComponent);
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toContain('Name');
  });

  it('updates the bound signal on input', () => {
    const f = TestBed.createComponent(HostComponent);
    f.detectChanges();
    const input: HTMLInputElement = f.nativeElement.querySelector('input');
    input.value = 'Ada';
    input.dispatchEvent(new Event('input'));
    f.detectChanges();
    expect(f.componentInstance.val()).toBe('Ada');
  });
});
