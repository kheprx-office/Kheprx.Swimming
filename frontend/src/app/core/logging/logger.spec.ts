import { createLogger, setLogLevel } from '@core/logging/logger';

describe('Logger', () => {
  afterEach(() => setLogLevel('debug'));

  it('emits through the matching console method with a tagged prefix', () => {
    const spy = jest.spyOn(console, 'warn').mockImplementation(() => {});
    const log = createLogger('usecase', 'GetPosts');
    log.warn('failed');
    expect(spy).toHaveBeenCalledTimes(1);
    expect(String(spy.mock.calls[0][0])).toContain('usecase');
    expect(String(spy.mock.calls[0][0])).toContain('GetPosts');
    spy.mockRestore();
  });

  it('suppresses levels below the active threshold', () => {
    const spy = jest.spyOn(console, 'log').mockImplementation(() => {});
    setLogLevel('warn');
    createLogger('core', 'x').debug('hidden');
    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });
});
