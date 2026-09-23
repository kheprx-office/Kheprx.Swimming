import { TestBed } from '@angular/core/testing';
import { LoadEnrollmentsUseCase } from '@features/championships/domain/usecases/load-enrollments.use-case';
import { SaveEnrollmentsUseCase } from '@features/championships/domain/usecases/save-enrollments.use-case';
import { CHAMPIONSHIPS_REPOSITORY } from '@features/championships/domain/repositories/championships.repository';

function make(repo: Record<string, unknown>) {
  TestBed.configureTestingModule({
    providers: [
      LoadEnrollmentsUseCase,
      SaveEnrollmentsUseCase,
      { provide: CHAMPIONSHIPS_REPOSITORY, useValue: repo },
    ],
  });
  return {
    load: TestBed.inject(LoadEnrollmentsUseCase),
    save: TestBed.inject(SaveEnrollmentsUseCase),
  };
}

describe('enrollment use-cases', () => {
  it('LoadEnrollments returns the id list', async () => {
    const { load } = make({ getEnrollments: async () => ({ data: ['s1', 's2'] }) });
    const res = await load.run('e1');
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual(['s1', 's2']);
  });

  it('LoadEnrollments fails on an invalid payload', async () => {
    const { load } = make({ getEnrollments: async () => ({ data: [1, 2] }) });
    const res = await load.run('e1');
    expect(res.ok).toBe(false);
  });

  it('SaveEnrollments succeeds on a 200 that carries no ids (server returns data: null) and echoes the submitted set', async () => {
    // The PUT endpoint replaces the set and returns ApiResponse<object> (data: null) — it does NOT echo the ids back.
    const setEnrollments = jest.fn().mockResolvedValue({ data: null });
    const { save } = make({ setEnrollments });
    const res = await save.run({ eventId: 'e1', swimmerIds: ['s1', 's2'] });
    expect(setEnrollments).toHaveBeenCalledWith('e1', { swimmerIds: ['s1', 's2'] });
    expect(res.ok).toBe(true);
    if (res.ok) expect(res.data).toEqual(['s1', 's2']);   // the just-saved set becomes the new baseline
  });

  it('SaveEnrollments fails when the PUT call rejects', async () => {
    const setEnrollments = jest.fn().mockRejectedValue(new Error('network'));
    const { save } = make({ setEnrollments });
    const res = await save.run({ eventId: 'e1', swimmerIds: ['s1'] });
    expect(res.ok).toBe(false);
  });
});
