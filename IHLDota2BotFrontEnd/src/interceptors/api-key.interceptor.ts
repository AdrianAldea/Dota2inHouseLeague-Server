import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AdminKeyService } from '../services/admin-key.service';

/**
 * Attaches the operator-supplied admin key as `X-Api-Key` on every backend
 * request. The Game Manager enforces the key only on its `/admin` routes and
 * ignores the header everywhere else, so sending it unconditionally keeps
 * this interceptor free of route knowledge. No key entered → requests pass
 * through untouched.
 */
export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  const key = inject(AdminKeyService).key();
  return key ? next(req.clone({ setHeaders: { 'X-Api-Key': key } })) : next(req);
};
