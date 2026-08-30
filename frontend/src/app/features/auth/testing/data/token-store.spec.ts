import { TestBed } from '@angular/core/testing';
import { TokenStore } from '@features/auth/data/token-store';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';

describe('TokenStore', () => {
  let store: TokenStore;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [TokenStore, KeyValueStore] });
    store = TestBed.inject(TokenStore);
  });
  it('saves, reads, and clears tokens', async () => {
    await store.save({ accessToken: 'a', refreshToken: 'r' });
    expect(await store.getAccess()).toBe('a');
    expect(await store.getRefresh()).toBe('r');
    await store.clear();
    expect(await store.getAccess()).toBeNull();
  });
});
