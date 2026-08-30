import { TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { NavButtonComponent } from '@core/ui/components/nav-button.component';

@Component({
  standalone: true,
  imports: [NavButtonComponent],
  template: `<app-nav-button [label]="'Go'" [disabled]="disabled" (press)="onPress()" />`,
})
class HostComponent {
  disabled = false;
  pressed = 0;
  onPress() { this.pressed++; }
}

describe('NavButtonComponent', () => {
  it('renders the label and emits press on click', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    expect(btn.textContent).toContain('Go');
    btn.click();
    expect(fixture.componentInstance.pressed).toBe(1);
  });

  it('does not emit when disabled', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.disabled = true;
    fixture.detectChanges();
    const btn: HTMLButtonElement = fixture.nativeElement.querySelector('button');
    btn.click();
    expect(fixture.componentInstance.pressed).toBe(0);
  });
});
