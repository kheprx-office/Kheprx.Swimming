import { TestBed } from '@angular/core/testing';
import { LoadingService } from './loading.service';

describe('LoadingService', () => {
  let svc: LoadingService;
  beforeEach(() => { svc = TestBed.inject(LoadingService); });

  it('is not loading initially', () => {
    expect(svc.isLoading()).toBe(false);
    expect(svc.activeCount()).toBe(0);
  });

  it('is loading after begin() and clears after the matching end()', () => {
    svc.begin();
    expect(svc.isLoading()).toBe(true);
    svc.end();
    expect(svc.isLoading()).toBe(false);
  });

  it('stays loading until all concurrent requests end', () => {
    svc.begin();
    svc.begin();
    expect(svc.activeCount()).toBe(2);
    svc.end();
    expect(svc.isLoading()).toBe(true);
    svc.end();
    expect(svc.isLoading()).toBe(false);
  });

  it('never underflows below zero', () => {
    svc.end();
    expect(svc.activeCount()).toBe(0);
    expect(svc.isLoading()).toBe(false);
  });
});
