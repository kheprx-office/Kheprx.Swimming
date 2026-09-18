import { TestBed } from '@angular/core/testing';
import { SelectFieldComponent } from '@core/ui/components/select-field.component';

describe('SelectFieldComponent', () => {
  it('exposes label + options and updates value model', () => {
    TestBed.configureTestingModule({ imports: [SelectFieldComponent] });
    const fixture = TestBed.createComponent(SelectFieldComponent);
    fixture.componentRef.setInput('label', 'Gender');
    fixture.componentRef.setInput('options', [{ id: 'g1', code: 'male', nameEn: 'Male', nameAr: 'ذكر' }]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.value.set('g1');
    expect(cmp.value()).toBe('g1');
    expect(cmp.options().length).toBe(1);
  });
});
