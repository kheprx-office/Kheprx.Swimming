import { TestBed } from '@angular/core/testing';
import { ChipGroupComponent } from '@core/ui/components/chip-group.component';

describe('ChipGroupComponent', () => {
  it('toggles ids in the selected model', () => {
    TestBed.configureTestingModule({ imports: [ChipGroupComponent] });
    const fixture = TestBed.createComponent(ChipGroupComponent);
    fixture.componentRef.setInput('options', [{ id: 's1', code: 'medley', nameEn: 'IM', nameAr: null }]);
    fixture.detectChanges();
    const cmp = fixture.componentInstance;
    cmp.toggle('s1');
    expect(cmp.selected()).toEqual(['s1']);
    cmp.toggle('s1');
    expect(cmp.selected()).toEqual([]);
  });
});
