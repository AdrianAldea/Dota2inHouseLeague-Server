import { Component, computed, signal } from '@angular/core';
import { CardComponent } from '../../components/card/card';
import { LobbyMonitorComponent } from '../../components/lobby-monitor/lobby-monitor';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'admin-page',
  standalone: true,
  imports: [CardComponent, LobbyMonitorComponent, FormsModule],
  templateUrl: './admin.html',
  styleUrl: './admin.css'
})
export class AdminPage {
  discordMessage = signal('');

  // Player lifecycle
  gameManagerCreateAuthorId = signal('');
  gameManagerCreateLobbyName = signal('');
  gameManagerCreateLobbyPassword = signal('');

  gameManagerRegisterSteamId = signal('');
  gameManagerRegisterUserId = signal('');
  gameManagerRegisterUsername = signal('');

  gameManagerLinkUserId = signal('');
  gameManagerLinkSteamId = signal('');
  gameManagerUnregisterId = signal('');

  // Lobby / invite / profile
  gameManagerReadyId = signal('');
  gameManagerPlayerId = signal('');
  gameManagerInvite = signal('');

  // Queue
  gameManagerJoinQueueId = signal('');
  gameManagerLeaveId = signal('');
  gameManagerUnreadyId = signal('');

  // Stats / leaderboard / rank
  gameManagerStatsId = signal('');
  gameManagerRankId = signal('');
  gameManagerLeaderboardTop = signal('10');

  // Match
  gameManagerCurrentMatchUserId = signal('');
  gameManagerRecentMatchesUserId = signal('');
  gameManagerRecentMatchesTop = signal('5');
  gameManagerMatchId = signal('');
  gameManagerReportUserId = signal('');
  gameManagerReportWinner = signal<'Radiant' | 'Dire'>('Radiant');

  // Challenge
  gameManagerChallengeChallengerId = signal('');
  gameManagerChallengeChallengedId = signal('');
  gameManagerAcceptId = signal('');
  gameManagerDeclineId = signal('');

  // Admin
  gameManagerBanId = signal('');
  gameManagerBanReason = signal('');
  gameManagerUnbanId = signal('');
  gameManagerKickId = signal('');
  gameManagerAddAccountUsername = signal('');
  gameManagerAddAccountPassword = signal('');

  // Session controls — wire LobbyPoolManager game-started / game-finished
  gameManagerSessionLobbyIdStart = signal('');
  gameManagerSessionLobbyIdFinish = signal('');

  // ── Template helpers ──

  /** Trims a string — used in the existing Invite card's startPayload expression. */
  readonly trim = (s: string) => s.trim();

  /**
   * Ban URL with optional `?reason=`. Inlining `encodeURIComponent` directly
   * inside the template isn't allowed by Angular's template expression rules.
   */
  readonly banEndpoint = computed(() => {
    const id = this.gameManagerBanId();
    const reason = this.gameManagerBanReason().trim();
    const q = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    return `/gamemanager/api/gamemanager/admin/ban/${id}${q}`;
  });

  /** Leaderboard URL with optional `?top=N` (default 10 server-side). */
  readonly leaderboardEndpoint = computed(() => {
    const top = this.gameManagerLeaderboardTop().trim();
    const q = top ? `?top=${encodeURIComponent(top)}` : '';
    return `/gamemanager/api/gamemanager/leaderboard${q}`;
  });

  /** Recent-matches URL with `?top=N` (default 5 server-side). */
  readonly recentMatchesEndpoint = computed(() => {
    const id = this.gameManagerRecentMatchesUserId();
    const top = this.gameManagerRecentMatchesTop().trim();
    const q = top ? `?top=${encodeURIComponent(top)}` : '';
    return `/gamemanager/api/gamemanager/match/recent/${id}${q}`;
  });
}
