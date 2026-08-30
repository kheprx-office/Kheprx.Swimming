// Seam interfaces that let CryptoService run without the real browser globals in
// tests. Production wires the global `crypto` (window.crypto) + an IndexedDB-backed
// key provider; tests inject Node's webcrypto + an in-memory key provider.

// The subset of the Web Crypto API the service depends on. Both window.crypto and
// Node's webcrypto satisfy this structurally.
export interface WebCryptoLike {
  getRandomValues<T extends ArrayBufferView>(array: T): T;
  subtle: {
    encrypt(
      algorithm: { name: 'AES-GCM'; iv: BufferSource },
      key: CryptoKey,
      data: BufferSource,
    ): Promise<ArrayBuffer>;
    decrypt(
      algorithm: { name: 'AES-GCM'; iv: BufferSource },
      key: CryptoKey,
      data: BufferSource,
    ): Promise<ArrayBuffer>;
  };
}

// Provides the AES-256-GCM key as a CryptoKey. Get-or-create, cached after first load.
export interface KeyProvider {
  getOrCreateKey(): Promise<CryptoKey>;
}
