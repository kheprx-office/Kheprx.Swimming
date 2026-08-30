import { webcrypto } from 'node:crypto';
import { CryptoService } from '@core/crypto/crypto.service';
import { KeyProvider, WebCryptoLike } from '@core/crypto/types';

const cryptoLike = webcrypto as unknown as WebCryptoLike;

class FakeKeyProvider implements KeyProvider {
  private key?: CryptoKey;
  async getOrCreateKey(): Promise<CryptoKey> {
    if (!this.key) {
      this.key = (await webcrypto.subtle.generateKey(
        { name: 'AES-GCM', length: 256 },
        true,
        ['encrypt', 'decrypt'],
      )) as CryptoKey;
    }
    return this.key;
  }
}

describe('CryptoService (Web Crypto)', () => {
  const svc = new CryptoService(new FakeKeyProvider(), cryptoLike);

  it('round-trips plaintext through encrypt/decrypt', async () => {
    const blob = await svc.encrypt('hello world');
    expect(typeof blob).toBe('string');
    await expect(svc.decrypt(blob)).resolves.toBe('hello world');
  });

  it('throws AppError(crypto) when the blob is tampered', async () => {
    const blob = await svc.encrypt('secret');
    const bytes = Buffer.from(blob, 'base64');
    bytes[bytes.length - 1] ^= 0xff; // flip a tag byte
    const tampered = bytes.toString('base64');
    await expect(svc.decrypt(tampered)).rejects.toMatchObject({ kind: 'crypto' });
  });

  it('throws AppError(crypto) when the blob is too short', async () => {
    await expect(svc.decrypt('AAAA')).rejects.toMatchObject({ kind: 'crypto' });
  });
});
