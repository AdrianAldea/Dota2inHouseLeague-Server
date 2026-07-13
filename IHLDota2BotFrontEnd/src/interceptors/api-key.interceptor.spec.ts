import { HttpRequest, HttpResponse, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, firstValueFrom, of } from 'rxjs';
import { apiKeyInterceptor } from './api-key.interceptor';
import { AdminKeyService } from '../services/admin-key.service';

describe('apiKeyInterceptor', () => {
  const req = new HttpRequest('GET', '/gamemanager/api/gamemanager/admin/sessions');

  /** Runs the interceptor in an injection context and captures the forwarded request. */
  function intercept(): { forwarded: () => HttpRequest<unknown>; result: Promise<HttpEvent<unknown>> } {
    let forwarded: HttpRequest<unknown> | undefined;
    const next: HttpHandlerFn = (r): Observable<HttpEvent<unknown>> => {
      forwarded = r;
      return of(new HttpResponse({ body: null }));
    };
    const result = firstValueFrom(
      TestBed.runInInjectionContext(() => apiKeyInterceptor(req, next)),
    );
    return { forwarded: () => forwarded!, result };
  }

  afterEach(() => localStorage.removeItem('ihl.adminApiKey'));

  it('leaves the request untouched when no key is set', async () => {
    const { forwarded, result } = intercept();
    await result;

    expect(forwarded().headers.has('X-Api-Key')).toBe(false);
  });

  it('attaches X-Api-Key when a key is set', async () => {
    TestBed.inject(AdminKeyService).setKey('secret-key');

    const { forwarded, result } = intercept();
    await result;

    expect(forwarded().headers.get('X-Api-Key')).toBe('secret-key');
  });
});
