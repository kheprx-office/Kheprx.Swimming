import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { LoadingBarComponent } from './loading-bar.component';
import { LoadingService } from '@core/network/loading.service';

function bar(fixture: { nativeElement: HTMLElement }): HTMLElement | null {
  return fixture.nativeElement.querySelector('[role="progressbar"]');
}

describe('LoadingBarComponent', () => {
  it('shows the bar after the show-delay while loading, and hides it when loading ends', fakeAsync(() => {
    const loading = TestBed.inject(LoadingService);
    const fixture = TestBed.createComponent(LoadingBarComponent);
    fixture.detectChanges();
    expect(bar(fixture)).toBeNull();

    loading.begin();
    fixture.detectChanges();
    tick(120);
    fixture.detectChanges();
    expect(bar(fixture)).toBeTruthy();

    loading.end();
    fixture.detectChanges();
    expect(bar(fixture)).toBeNull();
  }));

  it('does not flash for requests faster than the show-delay', fakeAsync(() => {
    const loading = TestBed.inject(LoadingService);
    const fixture = TestBed.createComponent(LoadingBarComponent);
    fixture.detectChanges();

    loading.begin();
    fixture.detectChanges();
    tick(50);       // completes before the 120ms delay
    loading.end();
    fixture.detectChanges();
    tick(120);
    fixture.detectChanges();
    expect(bar(fixture)).toBeNull();
  }));
});
