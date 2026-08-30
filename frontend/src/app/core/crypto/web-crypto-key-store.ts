// webCryptoKeyStore: the production KeyProvider. Generates a NON-EXTRACTABLE
// AES-256-GCM CryptoKey and persists it in IndexedDB (structured clone supports
// CryptoKey). The raw key bytes never leave the browser — stronger than the mobile
// Keychain approach, which had to hand back raw bytes.
import { createLogger } from '@core/logging/logger';
import { KeyProvider } from './types';

const log = createLogger('core', 'webCryptoKeyStore');

const DB_NAME = 'base-frontend-crypto';
const STORE = 'keys';
const KEY_ID = 'aes-256-gcm-master';

let cached: CryptoKey | null = null;

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

function idbGet(db: IDBDatabase, key: string): Promise<CryptoKey | undefined> {
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readonly');
    const req = tx.objectStore(STORE).get(key);
    req.onsuccess = () => resolve(req.result as CryptoKey | undefined);
    req.onerror = () => reject(req.error);
  });
}

function idbPut(db: IDBDatabase, key: string, value: CryptoKey): Promise<void> {
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(value, key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export const webCryptoKeyStore: KeyProvider = {
  async getOrCreateKey(): Promise<CryptoKey> {
    if (cached) {
      return cached;
    }
    const db = await openDb();
    const existing = await idbGet(db, KEY_ID);
    if (existing) {
      cached = existing;
      return cached;
    }
    const key = await crypto.subtle.generateKey(
      { name: 'AES-GCM', length: 256 },
      false, // non-extractable
      ['encrypt', 'decrypt'],
    );
    await idbPut(db, KEY_ID, key);
    log.debug('generated new master key');
    cached = key;
    return cached;
  },
};
