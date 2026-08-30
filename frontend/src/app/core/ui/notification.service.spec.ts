import { NotificationService } from '@core/ui/notification.service';

describe('NotificationService', () => {
  it('sets and clears the current notification', () => {
    const svc = new NotificationService();
    expect(svc.current()).toBeNull();
    svc.error('nope');
    expect(svc.current()).toEqual({ kind: 'error', message: 'nope' });
    svc.clear();
    expect(svc.current()).toBeNull();
  });

  it('auto-dismisses after 10 seconds', () => {
    jest.useFakeTimers();
    const svc = new NotificationService();
    svc.error('nope');
    jest.advanceTimersByTime(9_999);
    expect(svc.current()).not.toBeNull();
    jest.advanceTimersByTime(1);
    expect(svc.current()).toBeNull();
    jest.useRealTimers();
  });

  it('resets the auto-dismiss timer when a new notification arrives', () => {
    jest.useFakeTimers();
    const svc = new NotificationService();
    svc.error('first');
    jest.advanceTimersByTime(8_000);
    svc.success('second');
    jest.advanceTimersByTime(8_000); // 16s since 'first', only 8s since 'second'
    expect(svc.current()).toEqual({ kind: 'success', message: 'second' });
    jest.advanceTimersByTime(2_000); // now 10s since 'second'
    expect(svc.current()).toBeNull();
    jest.useRealTimers();
  });

  it('clear() cancels the pending auto-dismiss timer', () => {
    jest.useFakeTimers();
    const svc = new NotificationService();
    svc.error('nope');
    svc.clear();
    expect(svc.current()).toBeNull();
    jest.advanceTimersByTime(10_000); // must not throw or resurrect anything
    expect(svc.current()).toBeNull();
    jest.useRealTimers();
  });
});
