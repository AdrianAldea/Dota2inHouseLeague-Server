import { Component, DestroyRef, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Observable, catchError, forkJoin, of } from 'rxjs';
import { GameManagerService } from '../../services/game-manager.service';
import { ActiveSession, MatchPlayer, MatchSummary, QueueStatus } from '../../models/game.models';

@Component({
  selector: 'lobby-monitor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './lobby-monitor.html',
  styleUrl: './lobby-monitor.css'
})
export class LobbyMonitorComponent implements OnInit, OnDestroy {
  // ── Live data ──
  sessions = signal<ActiveSession[]>([]);
  queue = signal<QueueStatus | null>(null);

  loading = signal(false);
  lastUpdated = signal<Date | null>(null);
  error = signal('');

  // Per-lobby action status text, keyed by lobbyId.
  lobbyStatus = signal<Record<string, string>>({});

  // Queue-level controls
  queueStatusMsg = signal('');
  readyUserId = signal('');

  // Match details panel
  matchLookupId = signal('');
  match = signal<MatchSummary | null>(null);
  matchStatusMsg = signal('');
  matchTitle = signal('');

  autoRefresh = signal(false);
  private timer: ReturnType<typeof setInterval> | null = null;

  private readonly api = inject(GameManagerService);
  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    this.refresh();
  }

  ngOnDestroy(): void {
    this.stopTimer();
  }

  // ── Refresh ──

  refresh(): void {
    this.loading.set(true);
    this.error.set('');

    // Each request degrades independently: a failure surfaces in the shared
    // error banner while the other panel still updates.
    forkJoin({
      sessions: this.api.getSessions().pipe(
        catchError((err: HttpErrorResponse) => {
          this.error.set(this.errText(err));
          return of(null);
        })
      ),
      queue: this.api.getQueueStatus().pipe(
        catchError((err: HttpErrorResponse) => {
          this.error.set(this.errText(err));
          return of(null);
        })
      )
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ sessions, queue }) => {
        if (sessions) {
          this.sessions.set(sessions);
          this.pruneLobbyStatuses(sessions);
        }
        if (queue) {
          this.queue.set(queue);
        }
        this.lastUpdated.set(new Date());
        this.loading.set(false);
      });
  }

  toggleAutoRefresh(): void {
    const on = !this.autoRefresh();
    this.autoRefresh.set(on);
    if (on) {
      this.refresh();
      this.timer = setInterval(() => this.refresh(), 5000);
    } else {
      this.stopTimer();
    }
  }

  private stopTimer(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  // ── Per-lobby actions ──

  markStarted(lobbyId: string): void {
    this.lobbyAction(lobbyId, this.api.markGameStarted(lobbyId), 'Marking started…', 'Started ✅');
  }

  destroy(lobbyId: string): void {
    if (!confirm(`Destroy lobby ${this.shortId(lobbyId)}?\n\nThis ends the hosted game (sends the Dota GC a leave), cancels the match, releases the bot account, and reopens the queue.`)) {
      return;
    }
    this.lobbyAction(lobbyId, this.api.destroyLobby(lobbyId), 'Destroying…', 'Destroyed 💥');
  }

  viewMatch(lobbyId: string): void {
    this.fetchMatch(this.api.getMatchByLobby(lobbyId), `Lobby ${this.shortId(lobbyId)}`, 'No match found for this lobby.');
  }

  // ── Queue-level controls ──

  forceReady(): void {
    this.queueAction(this.api.forceStart(), 'Forcing ready…', 'Force-started ✅');
  }

  resetQueue(): void {
    if (!confirm('Reset the current queue lobby? All queued players are removed.')) return;
    this.queueAction(this.api.resetQueue(), 'Resetting…', 'Reset ✅');
  }

  markPlayerReady(): void {
    const id = this.readyUserId().trim();
    if (!id) {
      this.queueStatusMsg.set('Enter a user ID first.');
      return;
    }
    this.queueAction(this.api.markPlayerReady(id), 'Marking ready…', `Marked ${id} ready ✅`);
  }

  // ── Match details panel (manual lookup by match GUID) ──

  lookupMatch(): void {
    const id = this.matchLookupId().trim();
    if (!id) {
      this.matchStatusMsg.set('Enter a match UUID first.');
      return;
    }
    this.fetchMatch(this.api.getMatchById(id), `Match ${this.shortId(id)}`, 'No match found for that UUID.');
  }

  clearMatch(): void {
    this.match.set(null);
    this.matchStatusMsg.set('');
    this.matchTitle.set('');
  }

  // ── Shared request helpers ──

  /** POST for a specific lobby; progress/result lands in that lobby's status line. */
  private lobbyAction(lobbyId: string, request$: Observable<unknown>, pendingMsg: string, okMsg: string): void {
    this.setLobbyStatus(lobbyId, pendingMsg);
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.setLobbyStatus(lobbyId, okMsg);
        this.refresh();
      },
      error: (err: HttpErrorResponse) => this.setLobbyStatus(lobbyId, `Error ❌ ${this.errText(err)}`)
    });
  }

  /** POST for the queue panel; progress/result lands in the queue status line. */
  private queueAction(request$: Observable<unknown>, pendingMsg: string, okMsg: string): void {
    this.queueStatusMsg.set(pendingMsg);
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.queueStatusMsg.set(okMsg);
        this.refresh();
      },
      error: (err: HttpErrorResponse) => this.queueStatusMsg.set(`Error ❌ ${this.errText(err)}`)
    });
  }

  /** GET a match summary into the details panel, with a friendly 404 message. */
  private fetchMatch(request$: Observable<MatchSummary>, title: string, notFoundMsg: string): void {
    this.matchTitle.set(title);
    this.matchStatusMsg.set('Fetching…');
    this.match.set(null);
    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.match.set(res);
        this.matchStatusMsg.set('');
      },
      error: (err: HttpErrorResponse) => {
        this.matchStatusMsg.set(err.status === 404 ? notFoundMsg : `Error ❌ ${this.errText(err)}`);
      }
    });
  }

  private setLobbyStatus(lobbyId: string, msg: string): void {
    this.lobbyStatus.update((m) => ({ ...m, [lobbyId]: msg }));
  }

  /** Drop status lines for lobbies that no longer exist so the map can't grow unbounded. */
  private pruneLobbyStatuses(sessions: ActiveSession[]): void {
    const live = new Set(sessions.map((s) => s.lobbyId));
    this.lobbyStatus.update((m) =>
      Object.fromEntries(Object.entries(m).filter(([id]) => live.has(id)))
    );
  }

  private errText(err: HttpErrorResponse): string {
    if (typeof err.error === 'string' && err.error) return err.error;
    return err.message || `HTTP ${err.status}`;
  }

  // ── Template helpers ──

  shortId(id: string | null | undefined): string {
    if (!id) return '—';
    return id.length >= 8 ? id.slice(0, 8) : id;
  }

  stateLabel(state: number): string {
    return ['Ready', 'In Progress', 'Finished'][state] ?? `#${state}`;
  }

  stateClass(state: number): string {
    return ['state-ready', 'state-inprogress', 'state-finished'][state] ?? '';
  }

  matchStatusLabel(status: number): string {
    return ['In Progress', 'Completed', 'Cancelled'][status] ?? `#${status}`;
  }

  matchStatusClass(status: number): string {
    return ['state-inprogress', 'state-finished', 'state-cancelled'][status] ?? '';
  }

  winnerLabel(team: number | null): string {
    if (team === null || team === undefined) return 'Not scored';
    return team === 0 ? 'Radiant' : 'Dire';
  }

  playersOn(match: MatchSummary, team: number): MatchPlayer[] {
    return match.players.filter((p) => p.team === team);
  }
}
