// AppError: a typed error carrying the failure kind (and HTTP status / backend code when known).
export type AppErrorKind =
  | 'network'
  | 'http'
  | 'validation'
  | 'storage'
  | 'crypto'
  | 'auth'
  | 'unknown';

export class AppError extends Error {
  readonly kind: AppErrorKind;
  readonly status?: number;
  readonly code?: string;
  readonly serverMessage?: string;

  constructor(message: string, kind: AppErrorKind, status?: number, code?: string, serverMessage?: string) {
    super(message);
    this.name = 'AppError';
    this.kind = kind;
    this.status = status;
    this.code = code;
    this.serverMessage = serverMessage;
  }
}
