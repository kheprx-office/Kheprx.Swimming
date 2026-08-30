// HttpClientService: a typed wrapper over Angular HttpClient. Returns Promises (so
// the Promise-based UseCase layer stays simple) and maps failures to AppError.
// Transport failures (status 0) and timeouts are retried with exponential backoff;
// HTTP errors (4xx/5xx) are never retried.
import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, timer } from 'rxjs';
import { retry, timeout } from 'rxjs/operators';
import { env } from '@core/config/env';
import { createLogger } from '@core/logging/logger';
import { toAppError } from '@core/network/api/http-error';

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';
export interface HttpRequestOptions {
  body?: unknown;
  headers?: Record<string, string>;
  params?: Record<string, string | number | boolean>;
}

@Injectable({ providedIn: 'root' })
export class HttpClientService {
  private readonly http = inject(HttpClient);
  private readonly log = createLogger('core', 'apiClient');
  private readonly baseUrl = env.api.baseUrl;

  get<T>(path: string, opts?: HttpRequestOptions): Promise<T> {
    return this.request<T>('GET', path, opts);
  }
  post<T>(path: string, opts?: HttpRequestOptions): Promise<T> {
    return this.request<T>('POST', path, opts);
  }
  put<T>(path: string, opts?: HttpRequestOptions): Promise<T> {
    return this.request<T>('PUT', path, opts);
  }
  patch<T>(path: string, opts?: HttpRequestOptions): Promise<T> {
    return this.request<T>('PATCH', path, opts);
  }
  delete<T>(path: string, opts?: HttpRequestOptions): Promise<T> {
    return this.request<T>('DELETE', path, opts);
  }

  // Binary GET (task-photo content). Kept separate from request<T> because that method is
  // hard-wired to a JSON response type. Goes through the same HttpClient — and therefore the
  // same authInterceptor — as everything else, which is the entire point: photo content is
  // served from an authenticated endpoint (AD-5), so a raw fetch() or an <img src> would be
  // sent without the bearer token and 401.
  //
  // No retry: a 404 here means the photo row or the stored object is gone, and retrying a
  // thumbnail is not worth the delay. Transport failures surface as AppError('network').
  getBlob(path: string): Promise<Blob> {
    const url = `${this.baseUrl}${path}`;
    this.log.debug('→', 'GET(blob)', url);
    return new Promise<Blob>((resolve, reject) => {
      this.http
        .get(url, { responseType: 'blob' })
        .pipe(timeout(env.api.timeoutMs))
        .subscribe({
          next: (blob) => resolve(blob),
          error: (err) => reject(toAppError(err)),
        });
    });
  }

  private request<T>(method: HttpMethod, path: string, opts: HttpRequestOptions = {}): Promise<T> {
    const url = `${this.baseUrl}${path}`;
    let params = new HttpParams();
    for (const [k, v] of Object.entries(opts.params ?? {})) params = params.set(k, String(v));
    this.log.debug('→', method, url);

    return new Promise<T>((resolve, reject) => {
      (
        this.http.request<T>(method, url, {
          body: opts.body,
          headers: opts.headers,
          params,
        }) as Observable<unknown>
      )
        .pipe(
          timeout(env.api.timeoutMs),
          retry({
            count: env.api.maxRetries,
            delay: (error, attempt) => {
              if (!this.isTransient(error)) throw error;
              const ms = env.api.backoffMs[attempt - 1] ?? 0;
              this.log.warn(`retrying in ${ms}ms (attempt ${attempt + 1})`, url);
              return timer(ms);
            },
          }),
        )
        .subscribe({
          next: (v) => resolve(v as T),
          error: (err) => reject(toAppError(err)),
        });
    });
  }

  private isTransient(err: unknown): boolean {
    if (err instanceof HttpErrorResponse) return err.status === 0;
    return true; // rxjs TimeoutError / non-HTTP transport failure
  }
}
