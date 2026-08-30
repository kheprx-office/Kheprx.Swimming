import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { AuthSessionStore } from '@features/auth/presentation/auth-session.store';

describe('appConfig root DI wiring', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [...appConfig.providers] }));

  it('resolves the AuthSessionStore from the application root', () => {
    expect(() => TestBed.inject(AuthSessionStore)).not.toThrow();
  });
});
