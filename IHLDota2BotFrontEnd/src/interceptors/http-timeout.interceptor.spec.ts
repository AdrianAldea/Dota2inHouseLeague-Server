import { HttpErrorResponse, HttpRequest, HttpResponse } from '@angular/common/http';
import { NEVER, firstValueFrom, of } from 'rxjs';
import { httpTimeoutInterceptor } from './http-timeout.interceptor';
import { environment } from '../environments/environment';

describe('httpTimeoutInterceptor', () => {
  const req = new HttpRequest('GET', '/gamemanager/api/gamemanager/queue/status');

  afterEach(() => vi.useRealTimers());

  it('passes through a response that arrives in time', async () => {
    const response = new HttpResponse({ body: { ok: true } });
    const result = await firstValueFrom(httpTimeoutInterceptor(req, () => of(response)));
    expect(result).toBe(response);
  });

  it('maps a timeout to an HttpErrorResponse with status 0', async () => {
    vi.useFakeTimers();

    // next() never emits → the timeout operator must fire.
    const promise = firstValueFrom(httpTimeoutInterceptor(req, () => NEVER));
    const assertion = expect(promise).rejects.toBeInstanceOf(HttpErrorResponse);

    await vi.advanceTimersByTimeAsync(environment.httpTimeoutMs + 1);
    await assertion;

    await promise.catch((err: HttpErrorResponse) => {
      expect(err.status).toBe(0);
      expect(err.statusText).toBe('Timeout');
    });
  });
});
