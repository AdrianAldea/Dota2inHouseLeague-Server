import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { LobbyMonitorComponent } from './lobby-monitor';
import { GameManagerService } from '../../services/game-manager.service';
import { ActiveSession, QueueStatus } from '../../models/game.models';

function makeQueue(): QueueStatus {
  return {
    currentLobbyId: 'lobby-1',
    currentLobbyPlayerCount: 1,
    playersPerLobby: 2,
    playersReady: 0,
    totalRegistered: 3,
    waitingPlayers: 2,
    players: [{ username: 'alice', isReady: false }],
  };
}

function makeSession(lobbyId: string): ActiveSession {
  return { lobbyId, accountId: 'acc', playerCount: 2, state: 1, startedAt: '2026-01-01T00:00:00Z' };
}

describe('LobbyMonitorComponent', () => {
  let api: {
    getSessions: ReturnType<typeof vi.fn>;
    getQueueStatus: ReturnType<typeof vi.fn>;
  };

  function create(): LobbyMonitorComponent {
    TestBed.configureTestingModule({
      imports: [LobbyMonitorComponent],
      providers: [{ provide: GameManagerService, useValue: api }],
    });
    return TestBed.createComponent(LobbyMonitorComponent).componentInstance;
  }

  beforeEach(() => {
    api = {
      getSessions: vi.fn(() => of([makeSession('lobby-1')])),
      getQueueStatus: vi.fn(() => of(makeQueue())),
    };
  });

  it('refresh populates sessions and queue and clears loading', () => {
    const c = create();
    c.refresh();

    expect(c.sessions().length).toBe(1);
    expect(c.queue()?.currentLobbyId).toBe('lobby-1');
    expect(c.loading()).toBe(false);
    expect(c.lastUpdated()).not.toBeNull();
  });

  describe('label helpers', () => {
    it('maps known state/status/team values', () => {
      const c = create();
      expect(c.stateLabel(0)).toBe('Ready');
      expect(c.stateLabel(1)).toBe('In Progress');
      expect(c.matchStatusLabel(2)).toBe('Cancelled');
      expect(c.winnerLabel(0)).toBe('Radiant');
      expect(c.winnerLabel(1)).toBe('Dire');
      expect(c.winnerLabel(null)).toBe('Not scored');
    });

    it('falls back for unknown enum values', () => {
      const c = create();
      expect(c.stateLabel(9)).toBe('#9');
      expect(c.matchStatusLabel(9)).toBe('#9');
    });

    it('shortId truncates long ids and handles empty', () => {
      const c = create();
      expect(c.shortId('0123456789abcdef')).toBe('01234567');
      expect(c.shortId(null)).toBe('—');
      expect(c.shortId('abc')).toBe('abc');
    });
  });
});
