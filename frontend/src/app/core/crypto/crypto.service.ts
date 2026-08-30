// CryptoService: AES-256-GCM encrypt/decrypt of arbitrary strings — the web
// equivalent of the mobile CryptoService. The CryptoKey comes from an injected
// KeyProvider (IndexedDB in production); the AES work goes through an injected
// WebCryptoLike. Both seams keep this class testable on Node's webcrypto.
//
// Output format: Base64( iv(12 bytes) ‖ ciphertext+tag ). Unlike the mobile
// `iv ‖ ciphertext ‖ tag` split, Web Crypto APPENDS the 16-byte GCM auth tag to
// the ciphertext, so we keep only the 12-byte IV prefix and treat the rest as one.
import { AppError } from '@core/domain/errors/app-error';
import { createLogger } from '@core/logging/logger';
import { KeyProvider, WebCryptoLike } from './types';

const log = createLogger('core', 'CryptoService');

const IV_BYTES = 12; // GCM-recommended nonce length

export class CryptoService {
  constructor(
    private readonly keys: KeyProvider,
    private readonly crypto: WebCryptoLike,
  ) {}

  async encrypt(plain: string): Promise<string> {
    try {
      const key = await this.keys.getOrCreateKey();
      const iv = this.crypto.getRandomValues(new Uint8Array(IV_BYTES) as Uint8Array<ArrayBuffer>);
      const cipherBuf = await this.crypto.subtle.encrypt(
        { name: 'AES-GCM', iv },
        key,
        new TextEncoder().encode(plain),
      );
      const ciphertext = new Uint8Array(cipherBuf);
      const out = new Uint8Array(iv.length + ciphertext.length);
      out.set(iv, 0);
      out.set(ciphertext, iv.length);
      return toBase64(out);
    } catch (e) {
      log.warn('encrypt failed', e instanceof Error ? e.message : String(e));
      throw new AppError('Encryption failed', 'crypto');
    }
  }

  async decrypt(blob: string): Promise<string> {
    try {
      const raw = fromBase64(blob);
      if (raw.length <= IV_BYTES) {
        throw new Error('blob too short');
      }
      const iv = raw.subarray(0, IV_BYTES);
      const data = raw.subarray(IV_BYTES);
      const key = await this.keys.getOrCreateKey();
      const plainBuf = await this.crypto.subtle.decrypt(
        { name: 'AES-GCM', iv },
        key,
        data,
      );
      return new TextDecoder().decode(plainBuf);
    } catch (e) {
      log.warn('decrypt failed', e instanceof Error ? e.message : String(e));
      throw new AppError('Decryption failed', 'crypto');
    }
  }
}

// Base64 helpers over binary (btoa/atob exist in browsers and jsdom).
function toBase64(bytes: Uint8Array): string {
  let bin = '';
  for (const b of bytes) {
    bin += String.fromCharCode(b);
  }
  return btoa(bin);
}

function fromBase64(b64: string): Uint8Array<ArrayBuffer> {
  const bin = atob(b64);
  const out = new Uint8Array(bin.length) as Uint8Array<ArrayBuffer>;
  for (let i = 0; i < bin.length; i++) {
    out[i] = bin.charCodeAt(i);
  }
  return out;
}
