// languageInterceptor: tells the backend which language to localize responses in by attaching the
// active site language (en|ar) as the Accept-Language header on every request. It pairs with the
// backend's request-localization middleware, which resolves error/success messages from this header.
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LanguageStore } from './language.store';

export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  const lang = inject(LanguageStore).lang();
  return next(req.clone({ setHeaders: { 'Accept-Language': lang } }));
};
