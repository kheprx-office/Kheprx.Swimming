import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CaptainPanelPage } from '@features/captain-panel';

type Probe = { cards: { key: string; route?: string }[] };

function make(): Probe {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ imports: [CaptainPanelPage], providers: [provideRouter([])] });
  return TestBed.createComponent(CaptainPanelPage).componentInstance as unknown as Probe;
}

describe('CaptainPanelPage', () => {
  it('renders the four panel cards in order', () => {
    const keys = make().cards.map((c) => c.key);
    expect(keys).toEqual(['accountCreation', 'swimmerRecords', 'medicalTests', 'healthMonitoring']);
  });

  it('makes only Account Creation navigable; the rest are safe placeholders', () => {
    const cards = make().cards;
    expect(cards.find((c) => c.key === 'accountCreation')?.route).toBe('/captain-panel/account-creation');
    expect(cards.filter((c) => c.key !== 'accountCreation').every((c) => !c.route)).toBe(true);
  });
});
