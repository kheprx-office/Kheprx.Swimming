import { TestBed } from '@angular/core/testing';
import { DateFieldComponent } from '@core/ui/components/date-field.component';

describe('DateFieldComponent', () => {
  it('updates its value model', () => {
    TestBed.configureTestingModule({ imports: [DateFieldComponent] });
    const fixture = TestBed.createComponent(DateFieldComponent);
    const cmp = fixture.componentInstance;
    cmp.value.set('2010-05-01');
    expect(cmp.value()).toBe('2010-05-01');
  });

  it('applies the max attribute to the input when set, and omits it otherwise', () => {
    TestBed.configureTestingModule({ imports: [DateFieldComponent] });
    const fixture = TestBed.createComponent(DateFieldComponent);
    fixture.detectChanges();
    const input = () => fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input().getAttribute('max')).toBeNull();
    fixture.componentRef.setInput('max', '2026-09-25');
    fixture.detectChanges();
    expect(input().getAttribute('max')).toBe('2026-09-25');
  });
});
