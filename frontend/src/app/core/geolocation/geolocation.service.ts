// GeolocationService: wraps navigator.geolocation in a promise that never rejects.
// Callers branch on `ok` — the worker clock flow must show the demo's Arabic message
// rather than surface a raw PositionError.
import { Injectable } from '@angular/core';

export interface Coordinates {
  lat: number;
  lng: number;
}

export type GeolocationResult =
  { ok: true; coords: Coordinates } | { ok: false; reason: 'unsupported' | 'denied' | 'timeout' };

@Injectable({ providedIn: 'root' })
export class GeolocationService {
  getCurrentPosition(): Promise<GeolocationResult> {
    if (!navigator.geolocation) {
      return Promise.resolve({ ok: false, reason: 'unsupported' });
    }
    return new Promise<GeolocationResult>((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (pos) =>
          resolve({ ok: true, coords: { lat: pos.coords.latitude, lng: pos.coords.longitude } }),
        (err) => resolve({ ok: false, reason: err.code === err.TIMEOUT ? 'timeout' : 'denied' }),
        { enableHighAccuracy: true, timeout: 15000 },
      );
    });
  }
}
