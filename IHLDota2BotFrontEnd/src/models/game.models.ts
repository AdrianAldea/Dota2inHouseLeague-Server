/**
 * Wire shapes mirroring the backend Common DTOs.
 *
 * System.Text.Json serialises with camelCase keys and enums as numbers, so the
 * numeric `state` / `status` / `team` / `winningTeam` fields arrive as numbers
 * and are mapped to labels by the component template helpers rather than trusted
 * as strings.
 */

/** PoolSessionState: 0 Ready, 1 InProgress, 2 Finished. */
export type PoolSessionState = number;

/** MatchTeam: 0 Radiant, 1 Dire. */
export type MatchTeam = number;

/** MatchStatus: 0 InProgress, 1 Completed, 2 Cancelled. */
export type MatchStatus = number;

export interface ActiveSession {
  lobbyId: string;
  accountId: string;
  playerCount: number;
  state: PoolSessionState;
  startedAt: string;
  gameStartedAt?: string | null;
  finishedAt?: string | null;
}

export interface QueuePlayer {
  username: string;
  isReady: boolean;
}

export interface QueueStatus {
  currentLobbyId: string | null;
  currentLobbyPlayerCount: number;
  playersPerLobby: number;
  playersReady: number;
  totalRegistered: number;
  waitingPlayers: number;
  players: QueuePlayer[];
}

export interface MatchPlayer {
  userId: number;
  username: string;
  team: MatchTeam;
  won: boolean;
}

export interface MatchSummary {
  id: string;
  lobbyId: string | null;
  createdAt: string;
  completedAt: string | null;
  status: MatchStatus;
  winningTeam: MatchTeam | null;
  players: MatchPlayer[];
}
