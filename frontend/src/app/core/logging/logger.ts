// Logger: a layer-aware, level-aware logging utility used app-wide.
// Usage: const log = createLogger('core', 'apiClient'); log.debug('→ GET', url);
import { env } from '@core/config/env';

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

// The architectural layer a log originates from — lets you trace a flow
// (page → viewmodel → usecase → repository → datasource → core).
export type LogLayer =
  | 'page'
  | 'viewmodel'
  | 'usecase'
  | 'repository'
  | 'datasource'
  | 'core';

export type Logger = {
  debug: (...args: unknown[]) => void;
  info: (...args: unknown[]) => void;
  warn: (...args: unknown[]) => void;
  error: (...args: unknown[]) => void;
};

const PREFIX = 'BOG-Web';

const LAYER_EMOJI: Record<LogLayer, string> = {
  page: '📄',
  viewmodel: '🪟',
  usecase: '🎲',
  repository: '🗄️',
  datasource: '🛰️',
  core: '⚙️',
};

const LEVEL_ORDER: Record<LogLevel, number> = {
  debug: 0,
  info: 1,
  warn: 2,
  error: 3,
};

// Route each level to the matching console method at emit time (not load time)
// so spies/wrappers see the live method.
const SINK: Record<LogLevel, (...args: unknown[]) => void> = {
  debug: (...args) => console.log(...args),
  info: (...args) => console.info(...args),
  warn: (...args) => console.warn(...args),
  error: (...args) => console.error(...args),
};

let minLevel: LogLevel = env.isDev ? 'debug' : 'warn';

export function setLogLevel(level: LogLevel): void {
  minLevel = level;
}

const enabled = (level: LogLevel): boolean =>
  LEVEL_ORDER[level] >= LEVEL_ORDER[minLevel];

export function createLogger(layer: LogLayer, name: string): Logger {
  const tag = `${PREFIX} [${LAYER_EMOJI[layer]} ${layer}] ${name}`;
  const emit =
    (level: LogLevel) =>
    (...args: unknown[]): void => {
      if (enabled(level)) {
        SINK[level](tag, ...args);
      }
    };
  return {
    debug: emit('debug'),
    info: emit('info'),
    warn: emit('warn'),
    error: emit('error'),
  };
}
