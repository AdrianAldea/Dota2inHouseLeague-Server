import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { TimeoutError, catchError, throwError, timeout } from 'rxjs';
import { environment } from '../environments/environment';

/**
 * Applies a global request timeout and normalises failures so every component
 * sees an {@link HttpErrorResponse}. Without this a slow/hung backend would leave
 * requests pending forever, and a client-side {@link TimeoutError} would bypass
 * the components' `HttpErrorResponse`-shaped error handling.
 */
export const httpTimeoutInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    timeout(environment.httpTimeoutMs),
    catchError((err) => {
      if (err instanceof TimeoutError) {
        return throwError(() => new HttpErrorResponse({
          url: req.url,
          status: 0,
          statusText: 'Timeout',
          error: `Request timed out after ${environment.httpTimeoutMs} ms`,
        }));
      }
      return throwError(() => err);
    }),
  );
