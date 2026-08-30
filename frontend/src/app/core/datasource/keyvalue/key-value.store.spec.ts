import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';

describe('KeyValueStore (localStorage)', () => {
  let store: KeyValueStore;

  beforeEach(() => {
    localStorage.clear();
    store = new KeyValueStore();
  });

  it('round-trips a string', async () => {
    await store.setString('k', 'v');
    await expect(store.getString('k')).resolves.toBe('v');
  });

  it('returns null for a missing string key', async () => {
    await expect(store.getString('missing')).resolves.toBeNull();
  });

  it('round-trips an object', async () => {
    await store.setObject('o', { a: 1, b: 'x' });
    await expect(store.getObject<{ a: number; b: string }>('o')).resolves.toEqual({ a: 1, b: 'x' });
  });

  it('throws AppError(storage) when a stored object is corrupt', async () => {
    localStorage.setItem('bad', '{not json');
    await expect(store.getObject('bad')).rejects.toMatchObject({ kind: 'storage' });
  });

  it('falls back to the default for a corrupt primitive', async () => {
    localStorage.setItem('n', 'not-a-number');
    await expect(store.getInt('n', 7)).resolves.toBe(7);
  });

  it('removes a key', async () => {
    await store.setString('k', 'v');
    await store.remove('k');
    await expect(store.getString('k')).resolves.toBeNull();
  });
});
