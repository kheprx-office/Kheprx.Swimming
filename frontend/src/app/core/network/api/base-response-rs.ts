// BaseResponseRs: the minimal response envelope every response DTO inherits.
// `success`/`message` are optional and synthesized at the repository boundary
// until the backend actually sends them.
export interface BaseResponseRs<T> {
  data: T;
  success?: boolean;
  message?: string;
}
