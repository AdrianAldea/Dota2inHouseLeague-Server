import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GameManagerService } from './game-manager.service';
import { environment } from '../environments/environment';

describe('GameManagerService', () => {
  let service: GameManagerService;
  let httpMock: HttpTestingController;
  const base = environment.gameManagerApi;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [GameManagerService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(GameManagerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getSessions GETs the admin sessions endpoint', () => {
    service.getSessions().subscribe();
    const req = httpMock.expectOne(`${base}/admin/sessions`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('getQueueStatus GETs the queue status endpoint', () => {
    service.getQueueStatus().subscribe();
    const req = httpMock.expectOne(`${base}/queue/status`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('getMatchByLobby builds the by-lobby URL', () => {
    service.getMatchByLobby('lobby-7').subscribe();
    const req = httpMock.expectOne(`${base}/match/by-lobby/lobby-7`);
    expect(req.request.method).toBe('GET');
    req.flush({});
  });

  it('getMatchById builds the match URL', () => {
    service.getMatchById('abc-123').subscribe();
    httpMock.expectOne(`${base}/match/abc-123`).flush({});
  });

  it('markGameStarted POSTs to the game/started URL', () => {
    service.markGameStarted('L1').subscribe();
    const req = httpMock.expectOne(`${base}/admin/game/started/L1`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('destroyLobby POSTs to the game/destroy URL', () => {
    service.destroyLobby('L1').subscribe();
    const req = httpMock.expectOne(`${base}/admin/game/destroy/L1`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('forceStart POSTs to admin/force_start', () => {
    service.forceStart().subscribe();
    httpMock.expectOne(`${base}/admin/force_start`).flush({});
  });

  it('resetQueue POSTs to admin/reset_queue', () => {
    service.resetQueue().subscribe();
    httpMock.expectOne(`${base}/admin/reset_queue`).flush({});
  });

  it('markPlayerReady POSTs to ready/{id}', () => {
    service.markPlayerReady('42').subscribe();
    const req = httpMock.expectOne(`${base}/ready/42`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });
});
