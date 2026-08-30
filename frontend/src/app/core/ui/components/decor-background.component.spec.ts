import { TestBed } from '@angular/core/testing';
import { DecorBackgroundComponent } from '@core/ui/components/decor-background.component';

describe('DecorBackgroundComponent', () => {
  it('defaults to the page variant', () => {
    const fixture = TestBed.createComponent(DecorBackgroundComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.variant()).toBe('page');
    expect(fixture.nativeElement.querySelector('.bg-grid-dots')).toBeTruthy();
  });

  it('renders the auth variant when set', () => {
    const fixture = TestBed.createComponent(DecorBackgroundComponent);
    fixture.componentRef.setInput('variant', 'auth');
    fixture.detectChanges();
    expect(fixture.componentInstance.variant()).toBe('auth');
    // auth variant uses stronger blobs (bg-primary/30)
    expect(fixture.nativeElement.innerHTML).toContain('bg-primary/30');
  });
});
