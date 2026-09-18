import { TestBed } from '@angular/core/testing';
import { SwimmersPage } from '@features/swimmers/presentation/pages/swimmers/swimmers.page';
import { ListSwimmersUseCase } from '@features/swimmers/domain/usecases/list-swimmers.use-case';
import { SwimmerListItem } from '@features/swimmers/domain/model/swimmer';
import { ok } from '@core/domain/result/result';
import { LanguageStore } from '@core/i18n/language.store';

const sample: SwimmerListItem[] = [
  { id: '1', uid: 'SW-0001', nameEn: 'Alpha', nameAr: null, clubNameEn: 'Oasis Main', clubNameAr: null, gender: 'male', age: 15 },
  { id: '2', uid: 'SW-0002', nameEn: 'Bravo', nameAr: null, clubNameEn: 'Oasis North', clubNameAr: null, gender: 'female', age: 16 },
];

function setup(items: SwimmerListItem[]) {
  const run = jest.fn().mockResolvedValue(ok(items));
  TestBed.configureTestingModule({
    imports: [SwimmersPage],
    providers: [
      { provide: ListSwimmersUseCase, useValue: { run } },
      { provide: LanguageStore, useValue: { lang: () => 'en' } },
    ],
  });
  const fixture = TestBed.createComponent(SwimmersPage);
  fixture.detectChanges(); // ngOnInit -> load()
  return { fixture, run };
}

describe('SwimmersPage', () => {
  it('renders swimmers after load', async () => {
    const { fixture } = setup(sample);
    await fixture.whenStable();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Alpha');
    expect(text).toContain('SW-0001');
    expect(text).toContain('Bravo');
    expect(fixture.componentInstance.visible().length).toBe(2);
  });

  it('filters by gender client-side', async () => {
    const { fixture } = setup(sample);
    await fixture.whenStable();
    fixture.detectChanges();
    fixture.componentInstance.setGenderFilter('female');
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Bravo');
    expect(text).not.toContain('Alpha');
  });
});
