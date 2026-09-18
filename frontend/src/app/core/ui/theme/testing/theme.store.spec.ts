import { TestBed } from '@angular/core/testing';
import { ThemeStore } from '@core/ui/theme/theme.store';

describe('ThemeStore', () => {
  beforeEach(() => { localStorage.clear(); document.documentElement.classList.remove('dark'); TestBed.resetTestingModule(); });

  it('defaults to light', () => {
    const s = TestBed.inject(ThemeStore); TestBed.tick();
    expect(s.mode()).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('toggles to dark, applies the class, and persists', () => {
    const s = TestBed.inject(ThemeStore); s.toggle(); TestBed.tick();
    expect(s.mode()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('kheprx.theme')).toBe('dark');
  });

  it('set(dark) switches mode and applies the class', () => {
    const s = TestBed.inject(ThemeStore);
    s.set('dark'); TestBed.tick();
    expect(s.mode()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('reads the persisted theme on construction', () => {
    localStorage.setItem('kheprx.theme', 'dark');
    expect(TestBed.inject(ThemeStore).mode()).toBe('dark');
  });
});
