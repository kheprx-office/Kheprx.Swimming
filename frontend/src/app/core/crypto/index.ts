// Production composition of CryptoService: the global Web Crypto API + the
// IndexedDB-backed key provider. Importing this pulls in browser globals, so unit
// tests import CryptoService directly and inject fakes instead.
import { CryptoService } from './crypto.service';
import { webCryptoKeyStore } from './web-crypto-key-store';

export { CryptoService } from './crypto.service';
export type { KeyProvider, WebCryptoLike } from './types';

// The global `crypto` (window.crypto) satisfies WebCryptoLike structurally.
export const cryptoService = new CryptoService(webCryptoKeyStore, crypto);
