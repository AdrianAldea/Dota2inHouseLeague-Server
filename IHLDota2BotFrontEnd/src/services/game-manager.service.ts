import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { ActiveSession, MatchSummary, QueueStatus } from '../models/game.models';

/**
 * Single place that knows the Game Manager API surface and URL layout used by
 * the live operations panel. Components depend on this instead of hardcoding
 * `/gamemanager/...` paths, so the proxy prefix lives in one config value.
 */
@Injectable({ providedIn: 'root' })
export class GameManagerService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.gameManagerApi;

  // ── Reads ──

  getSessions(): Observable<ActiveSession[]> {
    return this.http.get<ActiveSession[]>(`${this.base}/admin/sessions`);
  }

  getQueueStatus(): Observable<QueueStatus> {
    return this.http.get<QueueStatus>(`${this.base}/queue/status`);
  }

  getMatchByLobby(lobbyId: string): Observable<MatchSummary> {
    return this.http.get<MatchSummary>(`${this.base}/match/by-lobby/${lobbyId}`);
  }

  getMatchById(matchId: string): Observable<MatchSummary> {
    return this.http.get<MatchSummary>(`${this.base}/match/${matchId}`);
  }

  // ── Per-lobby actions ──

  markGameStarted(lobbyId: string): Observable<unknown> {
    return this.http.post(`${this.base}/admin/game/started/${lobbyId}`, {});
  }

  destroyLobby(lobbyId: string): Observable<unknown> {
    return this.http.post(`${this.base}/admin/game/destroy/${lobbyId}`, {});
  }

  // ── Queue-level actions ──

  forceStart(): Observable<unknown> {
    return this.http.post(`${this.base}/admin/force_start`, {});
  }

  resetQueue(): Observable<unknown> {
    return this.http.post(`${this.base}/admin/reset_queue`, {});
  }

  markPlayerReady(userId: string): Observable<unknown> {
    return this.http.post(`${this.base}/ready/${userId}`, {});
  }
}
