// KeyValueStore: a typed wrapper over localStorage that owns serialization and
// error mapping for the whole app. Repositories use it instead of touching
// localStorage directly, so the storage try/catch lives here exactly once.
//
// Semantics (mirrors the mobile base):
//  - A failed op is logged and re-thrown as AppError('storage').
//  - A missing key returns null (string/object) or the caller's fallback (int/double).
//  - Corruption is asymmetric ON PURPOSE: a malformed object throws AppError('storage')
//    (structured data is worth surfacing), while a malformed primitive falls back to
//    the default (primitives stay lenient).
// The synchronous localStorage calls are wrapped in Promises to keep the async
// contract identical to the mobile AsyncStorage-backed store.
import { Injectable } from '@angular/core';
import { AppError } from '@core/domain/errors/app-error';
import { createLogger } from '@core/logging/logger';

const log = createLogger('datasource', 'KeyValueStore');

@Injectable({ providedIn: 'root' })
export class KeyValueStore {
  // Run a storage op, mapping any failure to a 'storage' AppError.
  private async guard<T>(op: string, fn: () => T): Promise<T> {
    try {
      return fn();
    } catch (e) {
      log.warn(`${op} failed`, e);
      throw new AppError(`Failed to ${op} data`, 'storage');
    }
  }

  // region strings
  setString(key: string, value: string): Promise<void> {
    log.debug('setString →', key);
    return this.guard('save', () => {
      localStorage.setItem(key, value);
    });
  }

  getString(key: string): Promise<string | null> {
    log.debug('getString ←', key);
    return this.guard('load', () => localStorage.getItem(key));
  }
  // endregion

  // region numbers (serialized as strings)
  setInt(key: string, value: number): Promise<void> {
    return this.setString(key, String(value));
  }

  async getInt(key: string, fallback = 0): Promise<number> {
    const raw = await this.getString(key);
    if (raw === null) {
      return fallback;
    }
    const n = Number(raw);
    return Number.isInteger(n) ? n : fallback;
  }

  setDouble(key: string, value: number): Promise<void> {
    return this.setString(key, String(value));
  }

  async getDouble(key: string, fallback = 0): Promise<number> {
    const raw = await this.getString(key);
    if (raw === null) {
      return fallback;
    }
    const n = Number(raw);
    return Number.isFinite(n) ? n : fallback;
  }
  // endregion

  // region objects
  setObject<T>(key: string, value: T): Promise<void> {
    return this.setString(key, JSON.stringify(value));
  }

  async getObject<T>(key: string): Promise<T | null> {
    const raw = await this.getString(key);
    if (raw === null) {
      return null;
    }
    try {
      return JSON.parse(raw) as T;
    } catch (e) {
      // Intentional asymmetry vs getInt/getDouble: corrupted structured data is a
      // real error worth surfacing, not silently defaulting.
      log.warn('getObject parse failed', key, e);
      throw new AppError('Failed to load data', 'storage');
    }
  }
  // endregion

  // region management
  remove(key: string): Promise<void> {
    log.debug('remove ✕', key);
    return this.guard('remove', () => {
      localStorage.removeItem(key);
    });
  }

  clear(): Promise<void> {
    log.debug('clear ✕ all');
    return this.guard('clear', () => {
      localStorage.clear();
    });
  }
  // endregion
}

// Shared non-DI instance (parity with the mobile singleton). Prefer DI in app code.
export const keyValueStore = new KeyValueStore();
