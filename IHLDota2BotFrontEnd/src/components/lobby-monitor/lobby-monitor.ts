import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';

// ── Wire shapes (match the Common DTOs) ──
// System.Text.Json serialises with camelCase keys and enums as numbers, so the
// numeric `state` / `status` / `team`/`winningTeam` fields are mapped to labels
// in the template helpers below rather than trusted as strings.

interface ActiveSession {
  lobbyId: string;
  accountId: string;
  playerCount: number;
  state: number; // PoolSessionState: 0 Ready, 1 InProgress, 2 Finished
  startedAt: string;
  gameStartedAt?: string | null;
  finishedAt?: string | null;
}

interface QueuePlayer {
  username: string;
  isReady: boolean;
}

interface QueueStatus {
  currentLobbyId: string | null;
  currentLobbyPlayerCount: number;
  playersPerLobby: number;
  playersReady: number;
  totalRegistered: number;
  waitingPlayers: number;
  players: QueuePlayer[];
}

interface MatchPlayer {
  userId: number;
  username: string;
  team: number; // MatchTeam: 0 Radiant, 1 Dire
  won: boolean;
}

interface MatchSummary {
  id: string;
  lobbyId: string | null;
  createdAt: string;
  completedAt: string | null;
  status: number; // MatchStatus: 0 InProgress, 1 Completed, 2 Cancelled
  winningTeam: number | null;
  players: MatchPlayer[];
}

const API = '/gamemanager/api/gamemanager';

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

  constructor(private http: HttpClient) {}

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

    this.http.get<ActiveSession[]>(`${API}/admin/sessions`).subscribe({
      next: (res) => {
        this.sessions.set(res ?? []);
        this.lastUpdated.set(new Date());
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(this.errText(err));
        this.loading.set(false);
      }
    });

    this.http.get<QueueStatus>(`${API}/queue/status`).subscribe({
      next: (res) => this.queue.set(res),
      error: (err) => this.error.set(this.errText(err))
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
    this.setLobbyStatus(lobbyId, 'Marking started…');
    this.http.post(`${API}/admin/game/started/${lobbyId}`, {}).subscribe({
      next: () => {
        this.setLobbyStatus(lobbyId, 'Started ✅');
        this.refresh();
      },
      error: (err) => this.setLobbyStatus(lobbyId, `Error ❌ ${this.errText(err)}`)
    });
  }

  destroy(lobbyId: string): void {
    if (!confirm(`Destroy lobby ${this.shortId(lobbyId)}?\n\nThis ends the hosted game (sends the Dota GC a leave), releases the bot account, and reopens the queue.`)) {
      return;
    }
    this.setLobbyStatus(lobbyId, 'Destroying…');
    this.http.post(`${API}/admin/game/destroy/${lobbyId}`, {}).subscribe({
      next: () => {
        this.setLobbyStatus(lobbyId, 'Destroyed 💥');
        this.refresh();
      },
      error: (err) => this.setLobbyStatus(lobbyId, `Error ❌ ${this.errText(err)}`)
    });
  }

  viewMatch(lobbyId: string): void {
    this.matchTitle.set(`Lobby ${this.shortId(lobbyId)}`);
    this.matchStatusMsg.set('Fetching…');
    this.match.set(null);
    this.http.get<MatchSummary>(`${API}/match/by-lobby/${lobbyId}`).subscribe({
      next: (res) => {
        this.match.set(res);
        this.matchStatusMsg.set('');
      },
      error: (err) => {
        this.matchStatusMsg.set(err?.status === 404 ? 'No match found for this lobby.' : `Error ❌ ${this.errText(err)}`);
      }
    });
  }

  // ── Queue-level controls ──

  forceReady(): void {
    this.queueStatusMsg.set('Forcing ready…');
    this.http.post(`${API}/admin/force_start`, {}).subscribe({
      next: () => {
        this.queueStatusMsg.set('Force-started ✅');
        this.refresh();
      },
      error: (err) => this.queueStatusMsg.set(`Error ❌ ${this.errText(err)}`)
    });
  }

  resetQueue(): void {
    if (!confirm('Reset the current queue lobby? All queued players are removed.')) return;
    this.queueStatusMsg.set('Resetting…');
    this.http.post(`${API}/admin/reset_queue`, {}).subscribe({
      next: () => {
        this.queueStatusMsg.set('Reset ✅');
        this.refresh();
      },
      error: (err) => this.queueStatusMsg.set(`Error ❌ ${this.errText(err)}`)
    });
  }

  markPlayerReady(): void {
    const id = this.readyUserId().trim();
    if (!id) {
      this.queueStatusMsg.set('Enter a user ID first.');
      return;
    }
    this.queueStatusMsg.set('Marking ready…');
    this.http.post(`${API}/ready/${id}`, {}).subscribe({
      next: () => {
        this.queueStatusMsg.set(`Marked ${id} ready ✅`);
        this.refresh();
      },
      error: (err) => this.queueStatusMsg.set(`Error ❌ ${this.errText(err)}`)
    });
  }

  // ── Match details panel (manual lookup by match GUID) ──

  lookupMatch(): void {
    const id = this.matchLookupId().trim();
    if (!id) {
      this.matchStatusMsg.set('Enter a match UUID first.');
      return;
    }
    this.matchTitle.set(`Match ${this.shortId(id)}`);
    this.matchStatusMsg.set('Fetching…');
    this.match.set(null);
    this.http.get<MatchSummary>(`${API}/match/${id}`).subscribe({
      next: (res) => {
        this.match.set(res);
        this.matchStatusMsg.set('');
      },
      error: (err) => {
        this.matchStatusMsg.set(err?.status === 404 ? 'No match found for that UUID.' : `Error ❌ ${this.errText(err)}`);
      }
    });
  }

  clearMatch(): void {
    this.match.set(null);
    this.matchStatusMsg.set('');
    this.matchTitle.set('');
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

  teamLabel(team: number): string {
    return team === 0 ? 'Radiant' : 'Dire';
  }

  winnerLabel(team: number | null): string {
    if (team === null || team === undefined) return 'Not scored';
    return team === 0 ? 'Radiant' : 'Dire';
  }

  playersOn(match: MatchSummary, team: number): MatchPlayer[] {
    return match.players.filter((p) => p.team === team);
  }

  setLobbyStatus(lobbyId: string, msg: string): void {
    this.lobbyStatus.update((m) => ({ ...m, [lobbyId]: msg }));
  }

  private errText(err: any): string {
    if (!err) return 'Unknown error';
    if (typeof err.error === 'string') return err.error;
    return err.message ?? `HTTP ${err.status ?? '?'}`;
  }
}
