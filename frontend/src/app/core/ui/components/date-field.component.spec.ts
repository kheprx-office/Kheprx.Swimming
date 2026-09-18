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
});
