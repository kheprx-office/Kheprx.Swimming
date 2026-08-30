import { Injectable, inject } from '@angular/core';
import { KeyValueStore } from '@core/datasource/keyvalue/key-value.store';
import { StorageKeys } from '@core/datasource/keyvalue/storage-keys';
import { AuthTokens } from '@features/auth/domain/model/shared/auth';

@Injectable({ providedIn: 'root' })
export class TokenStore {
  private readonly kv = inject(KeyValueStore);
  async save(t: AuthTokens): Promise<void> {
    await this.kv.setString(StorageKeys.accessToken, t.accessToken);
    await this.kv.setString(StorageKeys.refreshToken, t.refreshToken);
  }
  getAccess(): Promise<string | null> { return this.kv.getString(StorageKeys.accessToken); }
  getRefresh(): Promise<string | null> { return this.kv.getString(StorageKeys.refreshToken); }
  async clear(): Promise<void> {
    await this.kv.remove(StorageKeys.accessToken);
    await this.kv.remove(StorageKeys.refreshToken);
  }
}
